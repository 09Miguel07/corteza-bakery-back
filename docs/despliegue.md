# Despliegue de Corteza · Docker, Kubernetes, Azure y AWS

Taller R3-A2-S16 · Ingeniería de Software II. Esta guía lleva el ERP **Corteza** (API .NET 10 + front Angular 22 + SQL Server) desde el contenedor local hasta la nube.

## 1. Arquitectura desplegada

```
Navegador ──► web (nginx)  ──/api/*──►  api (ASP.NET, :8080)  ──►  SQL Server
              Angular estático          JWT por rol · /health        Azure SQL / RDS / contenedor
```

- El front llama siempre a `/api`; nginx lo reenvía a la API (`API_UPSTREAM`), así que **no hay CORS** que configurar.
- La API se configura solo con variables de entorno (sin tocar el código):

| Variable | Para qué |
|---|---|
| `ConnectionStrings__Corteza` | Cadena de conexión a SQL Server |
| `Jwt__Key` | Llave de firmas JWT, mínimo 32 caracteres (secreto) |
| `Seed__DefaultPassword` | Contraseña de los usuarios demo (secreto) |
| `Database__InitialiseOnStartup` | `true` = aplica migraciones y carga datos demo al arrancar |
| `Https__Redirect` | `false` si TLS lo termina el proxy/ingress (ya viene así en la imagen) |
| `API_UPSTREAM` (solo front) | URL base de la API, p. ej. `http://api:8080` |

- Sondeos de salud: API `GET /health`, front `GET /healthz`.

> ⚠️ Los valores del `docker-compose.yml` son solo de demostración. En la nube van como **secretos**.

## 2. Docker local (verificado)

Con los dos repos lado a lado (`corteza-bakery-back/` y `corteza-bakery/`):

```bash
cd corteza-bakery-back
docker compose up --build -d
```

- Front: http://localhost:8080 · API: http://localhost:5080/health
- Usuario demo: `laura.gomez` (admin). La contraseña es `Seed__DefaultPassword` del compose.
- Apagar: `docker compose down` (con `-v` borra también la base).

## 3. Imágenes en el registro

Los workflows `docker.yml` de cada repo publican en GitHub Container Registry al hacer push a `main`:

- `ghcr.io/09miguel07/corteza-api:latest`
- `ghcr.io/09miguel07/corteza-web:latest`

Tras el primer push, en GitHub → *Packages* → cada paquete → *Package settings* → **Change visibility → Public**, para que Azure/AWS/Kubernetes las descarguen sin credenciales (o configura un *pull secret*).

Manual, sin CI:

```bash
docker build -t ghcr.io/09miguel07/corteza-api:latest corteza-bakery-back
docker build -t ghcr.io/09miguel07/corteza-web:latest corteza-bakery
echo $GHCR_TOKEN | docker login ghcr.io -u 09Miguel07 --password-stdin
docker push ghcr.io/09miguel07/corteza-api:latest
docker push ghcr.io/09miguel07/corteza-web:latest
```

## 4. Kubernetes

Manifiestos en `k8s/` (namespace, SQL Server con volumen, API ×2, front ×2, ingress).

```bash
kubectl apply -f k8s/namespace.yaml
kubectl -n corteza create secret generic corteza-secrets \
  --from-literal=SA_PASSWORD='<clave SQL fuerte>' \
  --from-literal=JWT_KEY='<mínimo 32 caracteres>' \
  --from-literal=SEED_PASSWORD='<clave de los usuarios demo>'
kubectl apply -k k8s/
kubectl -n corteza get pods,svc,ingress
```

Probar sin nube (Docker Desktop con Kubernetes activo, kind o minikube): `kubectl -n corteza port-forward svc/corteza-web 8080:80` → http://localhost:8080.

Notas:
- SQL Server pide ~2 GB de RAM: usa nodos de al menos 2 vCPU / 4 GB.
- `ingress.yaml` asume un controlador `nginx`. En AKS: `az aks approuting enable`. En EKS: instala ingress-nginx o el AWS Load Balancer Controller y ajusta `ingressClassName`.
- En producción real reemplaza el SQL del clúster por **Azure SQL** o **Amazon RDS** y quita `sqlserver.yaml`.

## 5. Azure

Ruta recomendada para el taller: **Azure SQL + Azure Container Apps** (sin administrar servidores). Alternativa con Kubernetes: **AKS** (sección 4 sobre el clúster de Azure).

```bash
az login
RG=rg-corteza; LOC=eastus
az group create -n $RG -l $LOC

# 1) Base de datos (Azure SQL)
az sql server create -g $RG -n corteza-sql-<unico> -l $LOC -u cortezaadmin -p '<clave fuerte>'
az sql server firewall-rule create -g $RG -s corteza-sql-<unico> -n azure --start-ip-address 0.0.0.0 --end-ip-address 0.0.0.0
az sql db create -g $RG -s corteza-sql-<unico> -n Corteza --service-objective S0

# 2) Entorno de Container Apps
az extension add --name containerapp --upgrade
az containerapp env create -g $RG -n corteza-env -l $LOC

# 3) API
az containerapp create -g $RG -n corteza-api --environment corteza-env \
  --image ghcr.io/09miguel07/corteza-api:latest --target-port 8080 --ingress external \
  --min-replicas 1 \
  --secrets sql="Server=tcp:corteza-sql-<unico>.database.windows.net,1433;Initial Catalog=Corteza;User ID=cortezaadmin;Password=<clave fuerte>;Encrypt=True" \
            jwt="<mínimo 32 caracteres>" seed="<clave usuarios demo>" \
  --env-vars ConnectionStrings__Corteza=secretref:sql Jwt__Key=secretref:jwt \
             Seed__DefaultPassword=secretref:seed Database__InitialiseOnStartup=true

# 4) Front: apunta a la URL pública de la API
API_URL=$(az containerapp show -g $RG -n corteza-api --query properties.configuration.ingress.fqdn -o tsv)
az containerapp create -g $RG -n corteza-web --environment corteza-env \
  --image ghcr.io/09miguel07/corteza-web:latest --target-port 80 --ingress external \
  --env-vars API_UPSTREAM=https://$API_URL

# Enlace de entrega:
az containerapp show -g $RG -n corteza-web --query properties.configuration.ingress.fqdn -o tsv
```

Limpiar al terminar (evita cobros): `az group delete -n rg-corteza`.

> Azure SQL puede devolver errores transitorios. El código no activa `EnableRetryOnFailure` a propósito (choca con las transacciones manuales de inventario, ver `Infrastructure/DependencyInjection.cs`); si aparecen, envuelve esas transacciones con `CreateExecutionStrategy()`.

## 6. AWS

Ruta recomendada: **Amazon RDS for SQL Server + Amazon ECS Fargate** (o **App Runner** para la API). Alternativa con Kubernetes: **EKS**.

1. **RDS**: crea una instancia *SQL Server Express* (la más barata, entra en capa gratuita según la cuenta), sin acceso público salvo tu IP durante las pruebas. Cadena:
   `Server=<endpoint>,1433;Database=Corteza;User Id=admin;Password=<clave>;TrustServerCertificate=True`
2. **Secretos**: guarda la cadena, `Jwt__Key` y `Seed__DefaultPassword` en *Secrets Manager* o *Systems Manager Parameter Store*.
3. **ECS Fargate**: crea un clúster y dos servicios (`corteza-api` en el puerto 8080 y `corteza-web` en el 80) con las imágenes de GHCR (públicas). Un *Application Load Balancer* con reglas: `/api/*` → api, `/` → web. Con el ALB delante puedes poner `API_UPSTREAM` apuntando a su DNS o enrutar `/api` directo desde el balanceador.
4. **Health checks** del ALB: `/health` (api) y `/healthz` (web).
5. **Variables de la API**: las de la tabla de la sección 1, con los secretos inyectados desde Secrets Manager.
6. Entrega: el DNS del ALB (`http://<alb>.us-east-1.elb.amazonaws.com`).

Con EKS: `eksctl create cluster --name corteza --nodes 2 --node-type t3.medium` y después la sección 4.

## 7. Verificación después de desplegar

- [ ] `GET <api>/health` → `{"status":"ok"}`
- [ ] El front carga y `/login` funciona con `laura.gomez`
- [ ] Una pantalla con datos (Inventarios, Proveedores) muestra los datos demo
- [ ] Un POST real (crear un proveedor) persiste y sobrevive a un reinicio del contenedor
- [ ] Secretos fuera del repositorio y del historial de git
- [ ] Recursos de pruebas eliminados al cerrar el taller
