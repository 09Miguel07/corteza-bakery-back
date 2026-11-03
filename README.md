# Corteza ERP · Backend (.NET 10)

API REST de **Corteza**, el ERP para Panadería La Espiga — Ingeniería de Software II · Universidad de Cundinamarca, Extensión Chía.

Front: [09Miguel07/corteza-bakery](https://github.com/09Miguel07/corteza-bakery)

```
src/
├── Corteza.Domain/          Entidades, enums y reglas de negocio (sin dependencias)
├── Corteza.Application/     Servicios por módulo, DTOs con validación, excepciones
├── Corteza.Infrastructure/  EF Core + SQL Server, configuraciones, secuencias, seed
└── Corteza.Api/             Controllers, JWT por rol, manejo de errores
tests/
├── Corteza.Domain.Tests/      Unitarias de reglas de negocio (xUnit) · umbral de cobertura 80 %
├── Corteza.Application.Tests/ Servicios con EF Core InMemory + Moq (verifica que una regla rota no guarda)
└── Corteza.Specs/             BDD en Gherkin español (Reqnroll): proveedores, cotizaciones, OC, inventario
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

## Pruebas

Desarrollado con TDD: cada regla entra primero como prueba en rojo (`test(...): ... (red)`) y luego el código que la pasa (`feat(...): ... (green)`). Ver `git log --oneline`.

```bash
dotnet test                                                     # todo: unitarias + aplicación + BDD
dotnet test tests/Corteza.Domain.Tests                          # cobertura de proveedores/insumos/SC/OC: falla si baja de 80 %
dotnet test tests/Corteza.Specs --logger "console;verbosity=normal"   # escenarios Gherkin uno por uno
dotnet test --collect:"XPlat Code Coverage"                     # cobertura de las capas (coverlet, Cobertura XML)
```

| Proyecto | Qué prueba | Herramientas |
|---|---|---|
| `Corteza.Domain.Tests` | NIT y correo del proveedor, presupuesto, stock que nunca queda negativo, cotización ganadora, ciclo Pendiente → Aprobada → Recibida | xUnit, coverlet |
| `Corteza.Application.Tests` | Servicios contra base en memoria; `SaveChanges` no se llama cuando una regla falla | xUnit, EF Core InMemory, Moq |
| `Corteza.Specs` | Historias de usuario del taller como escenarios `Dado / Cuando / Entonces` | Reqnroll (sucesor de SpecFlow) |

La integración continua (`.github/workflows/ci.yml`) compila y corre las pruebas en cada push y pull request.

## Endpoints del incremento

| Método | Ruta | Uso |
|---|---|---|
| GET/POST | `/api/quotation-requests` | Listar / crear solicitudes de cotización (SC-001) |
| POST | `/api/quotation-requests/{id}/quotations` | Registrar la cotización de un proveedor |
| POST | `/api/quotation-requests/{id}/quotations/{qid}/select` | Elegir ganadora → crea la OC pendiente |
| POST | `/api/quotation-requests/{id}/cancel` | Cancelar la solicitud |
| GET/POST | `/api/purchase-orders` (+ `/summary`) | Listar / crear órdenes de compra |
| POST | `/api/purchase-orders/{id}/approve` | Aprobar: descuenta el presupuesto del proveedor |
| POST | `/api/purchase-orders/{id}/cancel` | Cancelar con motivo |
| POST | `/api/purchase-orders/{id}/receive` | Recibir: entra la mercancía al inventario |

Errores de negocio → `422` con `ProblemDetails`; conflicto de concurrencia → `409`.
