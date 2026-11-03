# Base de datos · SQL Server

El esquema lo crean las **migraciones de EF Core** (`api/`). Esta carpeta solo tiene cómo levantar el motor y un script de referencia para leer el modelo.

## Opción A · LocalDB (Windows, lo más simple)

Viene con Visual Studio (carga de trabajo *ASP.NET y desarrollo web* o *Almacenamiento y procesamiento de datos*). No hay que configurar nada: `appsettings.Development.json` ya apunta a:

```
Server=(localdb)\MSSQLLocalDB;Database=Corteza;Trusted_Connection=True;TrustServerCertificate=True
```

Comprobar que existe: `sqllocaldb info` → debe listar `MSSQLLocalDB`.

## Opción B · Docker (cualquier sistema)

```bash
cd db
docker compose up -d
```

Y en la API, sobrescribe la cadena con *user-secrets* (no se sube al repo):

```bash
cd api/src/Corteza.Api
dotnet user-secrets set "ConnectionStrings:Corteza" "Server=localhost,1433;Database=Corteza;User Id=sa;Password=Corteza_Dev2026!;TrustServerCertificate=True"
```

> ⚠️ `Corteza_Dev2026!` es solo para desarrollo local.

## Ver la base

- **SSMS** o **Azure Data Studio / extensión mssql de VS Code** → servidor `(localdb)\MSSQLLocalDB` o `localhost,1433`.
- `schema-reference.sql`: el modelo completo comentado (20 tablas). Si cambia una entidad, este archivo se actualiza a mano o se regenera con `dotnet ef migrations script`.
