# Corteza ERP · Backend (.NET 10)

API REST de **Corteza**, el ERP para Panadería La Espiga — Ingeniería de Software II · Universidad de Cundinamarca, Extensión Chía.

Front: [09Miguel07/corteza-bakery](https://github.com/09Miguel07/corteza-bakery)

```
src/
├── Corteza.Domain/          Entidades, enums y reglas de negocio (sin dependencias)
├── Corteza.Application/     Servicios por módulo, DTOs con validación, excepciones
├── Corteza.Infrastructure/  EF Core + SQL Server, configuraciones, secuencias, seed
└── Corteza.Api/             Controllers, JWT por rol, manejo de errores
db/                          SQL Server (LocalDB o Docker) y esquema de referencia
docs/                        Plan de implementación
```

## Arrancar

```bash
dotnet tool install --global dotnet-ef        # una vez
dotnet restore
dotnet ef migrations add InitialSchema -p src/Corteza.Infrastructure -s src/Corteza.Api -o Persistence/Migrations
dotnet run --project src/Corteza.Api           # http://localhost:5080/scalar
```

En `Development` la API aplica las migraciones y carga datos de demostración. Usuarios: `laura.gomez` (admin), `andres.pardo`, `sofia.diaz`, `ana.torres`, `camila.ortiz`, `diego.rojas`, `paula.gomez` — contraseña `Corteza2026*` (solo desarrollo).

Base de datos: ver `db/README.md` (LocalDB en Windows o `docker compose up -d`).
