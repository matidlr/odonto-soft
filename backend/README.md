# Odonto SaaS — Backend (.NET 8)

Estructura Clean Architecture:

```
src/
  Odonto.Domain/         Entidades y reglas de negocio puras (sin dependencias externas)
  Odonto.Application/    Interfaces y casos de uso (se va a ir llenando con CQRS/MediatR)
  Odonto.Infrastructure/ EF Core, DbContext, resolución de tenant
  Odonto.Api/             Controllers, JWT, Program.cs, Dockerfile
```

## Cómo correrlo localmente (sin Docker)

Requisitos: .NET 8 SDK, PostgreSQL corriendo en `localhost:5432` (o ajustar `appsettings.Development.json`).

```bash
# Desde la carpeta backend/

# 1. Armar el .sln (no viene incluido; se genera una sola vez)
dotnet new sln -n Odonto
dotnet sln add src/Odonto.Domain/Odonto.Domain.csproj
dotnet sln add src/Odonto.Application/Odonto.Application.csproj
dotnet sln add src/Odonto.Infrastructure/Odonto.Infrastructure.csproj
dotnet sln add src/Odonto.Api/Odonto.Api.csproj

# 2. Restaurar paquetes
dotnet restore

# 3. Instalar la herramienta de migraciones (una sola vez, global)
dotnet tool install --global dotnet-ef

# 4. Crear la primera migración
dotnet ef migrations add Inicial -p src/Odonto.Infrastructure -s src/Odonto.Api

# 5. Levantar la API (aplica migraciones automáticamente en Development)
dotnet run --project src/Odonto.Api
```

La API queda en `https://localhost:5001` (o el puerto que indique la consola) con Swagger en `/swagger`, y un endpoint de salud sin autenticación en `/health`.

## Cómo correrlo con Docker

Desde la raíz del repo (donde está `docker-compose.yml`):

```bash
docker compose up --build
```

Eso levanta Postgres + la API. La API queda expuesta en `http://localhost:5000`, con `/health` como chequeo rápido.

Nota: dentro del contenedor las migraciones también se aplican automáticamente al arrancar (solo en `ASPNETCORE_ENVIRONMENT=Development`, que es lo que usa el `docker-compose.yml`).

## Nota sobre este scaffold

Este esqueleto se armó a mano (sin ejecutar `dotnet` para generarlo, ni `dotnet build` para verificarlo) porque el entorno donde lo generé no tenía el SDK disponible en el momento. Antes de seguir construyendo sobre esto, corré `dotnet restore` y `dotnet build` para confirmar que compila; si aparece algún error de paquete o versión, avisame para corregirlo.

## Qué sigue

- `Odonto.Application`: casos de uso reales (crear tenant, invitar odontólogo, registrar paciente vía link) con MediatR + FluentValidation.
- `AuthController` con login real (ASP.NET Identity o hashing propio) que emita el JWT con los claims `tenant_id` y `rol`.
- Entidades de agenda: `Disponibilidad`, `Turno`, `TipoTratamiento`.
- Entidades de odontograma: `PiezaDental`, `HallazgoDental`, `PlanTratamiento` (ver el documento de arquitectura).
- Integración Mercado Pago (`Odonto.Infrastructure/Payments`).
