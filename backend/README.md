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

## Secretos (nunca van al repo)

`appsettings.json` y `appsettings.Development.json` quedan en el repo (público
o no, da igual: nunca hay que confiar en eso) con los secretos reales vacíos
o en placeholder. Los valores de verdad —contraseña de MySQL, `Jwt:Key`,
`Bootstrap:Key`, `Brevo:ApiKey`, `MercadoPago:AccessToken`— se cargan en la
máquina de cada uno con `dotnet user-secrets`, que los guarda fuera de la
carpeta del repo (en Windows: `%APPDATA%\Microsoft\UserSecrets\<id>\secrets.json`)
así es imposible subirlos por accidente con un `git add`.

La app se conecta con un usuario de MySQL propio (`odonto_app`), nunca con
`root`: tiene permisos solo sobre la base `odonto`, no sobre el resto del
servidor (ver sección "Usuario de MySQL de la aplicación" más abajo).

```bash
# Desde src/Odonto.Api/ (una sola vez, ya está inicializado en el .csproj)
dotnet user-secrets set "ConnectionStrings:Default" "Server=localhost;Port=3306;Database=odonto;User=odonto_app;Password=TU_PASSWORD_REAL"
dotnet user-secrets set "Jwt:Key" "una-clave-larga-y-random-de-al-menos-32-caracteres"
dotnet user-secrets set "Bootstrap:Key" "otra-clave-random-para-crear-el-primer-superadmin"
dotnet user-secrets set "Brevo:ApiKey" "tu-api-key-de-brevo"
dotnet user-secrets set "Brevo:SenderEmail" "no-responder@tudominio.com"
dotnet user-secrets set "MercadoPago:AccessToken" "tu-access-token-de-mercado-pago"

# Archivos:ClaveCifrado: 32 bytes en base64 (AES-256) para cifrar radiografías/
# PDFs en disco. Generarla UNA sola vez y no perderla (si se pierde o se
# cambia, los archivos ya cifrados con la vieja quedan ilegibles):
#   PowerShell: [Convert]::ToBase64String([byte[]](1..32 | ForEach-Object { Get-Random -Maximum 256 }))
dotnet user-secrets set "Archivos:ClaveCifrado" "la-clave-de-32-bytes-en-base64-que-generaste"

# Ver qué hay cargado (no muestra nada si todavía no configuraste nada)
dotnet user-secrets list
```

En producción (un servidor de verdad, no la compu local) no se usa
`user-secrets` — ahí van como variables de entorno del sistema/proceso, con
el mismo nombre pero `:` reemplazado por `__` (doble guion bajo), por ejemplo
`Jwt__Key`, `ConnectionStrings__Default`. `Program.cs` ya corta el arranque
si `Jwt:Key` quedó con el valor de ejemplo del repo fuera de Development, así
que no hay riesgo de desplegar sin haber puesto la clave real.

## Usuario de MySQL de la aplicación

La API nunca se conecta como `root`: usa un usuario dedicado (`odonto_app`)
que solo tiene permisos sobre la base `odonto`, no sobre el resto del
servidor (no puede ver ni tocar otras bases, ni crear usuarios, ni apagar
el servidor). Se crea una sola vez, conectado como root:

```sql
CREATE USER 'odonto_app'@'localhost' IDENTIFIED BY 'PASSWORD_FUERTE_DEL_APP';
GRANT ALL PRIVILEGES ON odonto.* TO 'odonto_app'@'localhost';
FLUSH PRIVILEGES;
```

`ALL PRIVILEGES ON odonto.*` alcanza para que la app lea/escriba datos y
para que EF Core cree/altere tablas al aplicar migraciones (en Development,
automático al hacer `dotnet run`) — pero está limitado a esa única base.
Los scripts de backup (`backup-db.ps1`/`restaurar-prueba.ps1`) siguen usando
`root` vía `mysql-backup.cnf` a propósito: son scripts administrativos que
corre una persona a mano, no la aplicación, y `restaurar-prueba.ps1`
necesita poder crear una base nueva (`CREATE DATABASE`), algo que
`odonto_app` no puede hacer ni debería.

## Logs

El logging lo maneja Serilog (configurado en `Program.cs`, no en `appsettings.json`). Se escribe en dos lugares a la vez:

- **Consola**: lo que ves mientras corre `dotnet run`.
- **Archivo**: `src/Odonto.Api/logs/odonto-AAAAMMDD.log` (un archivo por día, se guardan los últimos 30 días y después se borran solos). Este archivo persiste aunque cierres la terminal o reinicies el backend — es lo que hay que mirar si un usuario dice "el sistema no funciona" y ya pasó el momento.

Cada línea de log de un request incluye método, path, código de estado y tiempo de respuesta. Los logs de la aplicación (los que arrancan con `_logger.LogWarning(...)`, etc., en los controllers) llevan además `TenantId` y `UsuarioId` de quién hizo el request, para poder rastrear qué pasó en una clínica puntual.

La carpeta `logs/` nunca va al repo (está en `.gitignore`).

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
