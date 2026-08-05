using System.IdentityModel.Tokens.Jwt;
using System.Text;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Odonto.Api.Authorization;
using Odonto.Api.Logging;
using Odonto.Infrastructure;
using Odonto.Infrastructure.Persistence;
using Serilog;
using Serilog.Events;

// Por defecto, .NET remapea nombres de claims "conocidos" (sub, etc.) a URIs
// largas de esquemas antiguos al validar el JWT. Lo desactivamos para que
// los claims queden exactamente como los emitimos ("sub", "rol", "tenant_id" —
// el JWT no lleva email ni otros datos personales, ver AuthController).
JwtSecurityTokenHandler.DefaultMapInboundClaims = false;

var builder = WebApplication.CreateBuilder(args);

// Logging estructurado con Serilog: consola (para cuando corrés con dotnet
// run) + archivo en disco que persiste entre reinicios (para poder
// responder "¿qué pasó?" días después, no solo mientras la terminal está
// abierta). Un archivo por día, se guardan los últimos 30 días.
// Microsoft.AspNetCore/EntityFrameworkCore bajan a Warning para no llenar
// el log de ruido (SQL de cada consulta, etc.) — lo importante es errores,
// excepciones, y el resumen de cada request (método, path, status, tiempo).
builder.Host.UseSerilog((context, services, configuration) => configuration
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
    .MinimumLevel.Override("Microsoft.EntityFrameworkCore", LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .WriteTo.Console(
        outputTemplate: "{Timestamp:HH:mm:ss} [{Level:u3}] {Message:lj} (Tenant={TenantId} Usuario={UsuarioId}){NewLine}{Exception}")
    .WriteTo.File(
        Path.Combine(context.HostingEnvironment.ContentRootPath, "logs", "odonto-.log"),
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 30,
        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {Message:lj} (Tenant={TenantId} Usuario={UsuarioId}){NewLine}{Exception}"));

builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        // Enums como texto ("Cancelado") en vez de números, tanto en las
        // respuestas como en lo que se puede mandar en el body de un request.
        options.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
    });
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    // Permite probar endpoints protegidos desde Swagger pegando el JWT
    // que devuelve /api/auth/login (botón "Authorize" arriba a la derecha).
    options.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Description = "Pegar solo el token (sin la palabra 'Bearer')."
    });
    options.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
    {
        {
            new Microsoft.OpenApi.Models.OpenApiSecurityScheme
            {
                Reference = new Microsoft.OpenApi.Models.OpenApiReference
                {
                    Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

var jwtKey = builder.Configuration["Jwt:Key"]
    ?? throw new InvalidOperationException("Falta configurar Jwt:Key en appsettings o en variables de entorno.");

// El valor que viene de fábrica en appsettings.json (repo público) es un
// placeholder a propósito. Si alguna vez se despliega sin pisarlo por
// variable de entorno / secret manager, cualquiera que vea el repo podría
// firmar JWTs válidos. En Development lo dejamos pasar con un aviso; en
// cualquier otro ambiente, directamente no arranca.
const string jwtKeyPlaceholder = "CHANGE_ME_super_secret_key_min_32_chars_prod";
if (jwtKey == jwtKeyPlaceholder)
{
    if (!builder.Environment.IsDevelopment())
    {
        throw new InvalidOperationException(
            "Jwt:Key sigue con el valor de ejemplo del repo. Configurá una clave real y secreta " +
            "(variable de entorno o secret manager) antes de desplegar.");
    }
    Console.WriteLine("ADVERTENCIA: Jwt:Key todavía es el valor de ejemplo del repo. No usar así en producción.");
}
if (jwtKey.Length < 32)
{
    throw new InvalidOperationException("Jwt:Key debe tener al menos 32 caracteres para ser segura.");
}

// Misma lógica que Jwt:Key: la clave de cifrado de archivos (radiografías,
// PDFs) nunca puede quedar en appsettings.json, y validamos acá al arrancar
// en vez de dejar que el primer upload/descarga explote con un error confuso.
var claveCifradoArchivos = builder.Configuration["Archivos:ClaveCifrado"]
    ?? throw new InvalidOperationException(
        "Falta configurar Archivos:ClaveCifrado (dotnet user-secrets). Ver README para generarla.");
try
{
    var claveCifradoBytes = Convert.FromBase64String(claveCifradoArchivos);
    if (claveCifradoBytes.Length != 32)
    {
        throw new InvalidOperationException(
            $"Archivos:ClaveCifrado tiene que decodificar a 32 bytes (AES-256); tiene {claveCifradoBytes.Length}.");
    }
}
catch (FormatException)
{
    throw new InvalidOperationException("Archivos:ClaveCifrado tiene que ser texto en base64 válido.");
}

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
            // Nuestro claim de rol se llama "rol" (no el estándar), hay que
            // decírselo a ASP.NET Core para que [Authorize(Roles=...)] funcione.
            RoleClaimType = "rol"
        };
    });

builder.Services.AddAuthorization(options =>
{
    // Política que exigen los endpoints de negocio (pacientes, turnos, etc.):
    // el tenant del usuario tiene que estar Activo (o ser SuperAdmin).
    options.AddPolicy("TenantActivo", policy => policy.Requirements.Add(new TenantActivoRequirement()));

    // "Denegado por defecto": cualquier endpoint que no tenga [Authorize] ni
    // [AllowAnonymous] exige sesión igual. Así, si el día de mañana se
    // agrega un controller nuevo y alguien se olvida de decorarlo, no
    // queda expuesto sin querer — [AllowAnonymous] lo sigue pudiendo abrir
    // a propósito (auth, registro público, webhooks, /health).
    options.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
});
builder.Services.AddScoped<IAuthorizationHandler, TenantActivoHandler>();

// Límite de intentos para endpoints sensibles a fuerza bruta / spam
// (login, registro, recuperación de contraseña): 5 pedidos por minuto por
// IP. Sin esto, nada impide probar contraseñas en loop o inundar de
// emails de reseteo a una casilla ajena.
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddFixedWindowLimiter("auth", opt =>
    {
        opt.PermitLimit = 5;
        opt.Window = TimeSpan.FromMinutes(1);
        opt.QueueLimit = 0;
    });
});

var corsAllowedOrigin = builder.Configuration["Cors:AllowedOrigin"] ?? "http://localhost:4200";

// Nunca en producción con el origen del frontend en http://. Localhost en
// Development queda exceptuado porque ahí ni el propio backend habla https
// (ver comentario más abajo sobre UseHttpsRedirection).
if (!builder.Environment.IsDevelopment() && !corsAllowedOrigin.StartsWith("https://"))
{
    throw new InvalidOperationException(
        "Cors:AllowedOrigin tiene que ser https:// fuera de Development. Valor actual: " + corsAllowedOrigin);
}

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        // AllowCredentials es necesario para que el navegador mande/reciba
        // la cookie httpOnly del refresh token entre front y back (son
        // orígenes distintos: puertos diferentes). Por eso no se puede
        // combinar con AllowAnyOrigin, tiene que ser un origen puntual.
        policy.WithOrigins(corsAllowedOrigin)
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

var app = builder.Build();

// Manejo centralizado de errores no capturados: siempre quedan registrados
// en el log (con stack trace completo para debug), pero el cliente nunca
// recibe esos detalles — solo un mensaje genérico. En Development se ve
// además la página de diagnóstico de ASP.NET Core para debuggear rápido.
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}
else
{
    app.UseExceptionHandler(errorApp =>
    {
        errorApp.Run(async context =>
        {
            var feature = context.Features.Get<IExceptionHandlerFeature>();
            if (feature?.Error is not null)
            {
                var logger = context.RequestServices.GetRequiredService<ILoggerFactory>()
                    .CreateLogger("GlobalExceptionHandler");
                // TenantId/UsuarioId se leen directo del claim acá (en vez de
                // depender del LogContext de más abajo en el pipeline) porque
                // una excepción puede cortar la ejecución antes de que ese
                // middleware llegue a "empujar" esas propiedades.
                //
                // Method y Path vienen del request, así que en teoría los
                // controla quien nos manda el pedido — se sanean (se sacan
                // \r y \n) antes de loguearlos para que nadie pueda inyectar
                // saltos de línea que simulen líneas de log falsas.
                logger.LogError(feature.Error, "Error no controlado en {Method} {Path} (Tenant={TenantId} Usuario={UsuarioId})",
                    SaneadorLogs.Limpiar(context.Request.Method), SaneadorLogs.Limpiar(context.Request.Path.Value),
                    context.User.FindFirst("tenant_id")?.Value, context.User.FindFirst("sub")?.Value);
            }

            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsJsonAsync(new
            {
                message = "Ocurrió un error inesperado. Probá de nuevo en un momento."
            });
        });
    });

    // Sin certificado https en el perfil de Development (Kestrel solo
    // escucha por http en local), así que esto queda restringido a otros
    // ambientes para no romper el flujo de desarrollo.
    app.UseHsts();
    app.UseHttpsRedirection();
}

// Headers de seguridad básicos en todas las respuestas. La CSP queda
// afuera de Development porque Swagger UI necesita cargar sus propios
// scripts/estilos y una política estricta se los bloquearía.
app.Use(async (context, next) =>
{
    context.Response.Headers["X-Content-Type-Options"] = "nosniff";
    context.Response.Headers["X-Frame-Options"] = "DENY";
    context.Response.Headers["Referrer-Policy"] = "no-referrer";
    context.Response.Headers["Permissions-Policy"] = "geolocation=(), camera=(), microphone=()";
    if (!app.Environment.IsDevelopment())
    {
        context.Response.Headers["Content-Security-Policy"] = "default-src 'none'";
    }
    await next();
});

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();

    // Aplica migraciones pendientes automáticamente solo en desarrollo.
    // En producción las migraciones se corren como paso explícito del deploy.
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();
}

// Log de una línea por cada request (método, path, status, tiempo de
// respuesta), con TenantId/UsuarioId leídos directo del claim: no depende
// del middleware de más abajo, así que sale igual sin importar en qué
// punto del pipeline haya fallado o terminado el request.
app.UseSerilogRequestLogging(options =>
{
    options.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
    {
        diagnosticContext.Set("TenantId", httpContext.User.FindFirst("tenant_id")?.Value);
        diagnosticContext.Set("UsuarioId", httpContext.User.FindFirst("sub")?.Value);
    };
});

app.UseCors();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

// A partir de acá el usuario ya está autenticado: "empujamos" TenantId y
// UsuarioId al contexto de logging de Serilog para que TODOS los logs que
// se generen mientras se procesa este request (los de cualquier controller,
// sin tener que tocarlos uno por uno) queden etiquetados con quién y de qué
// clínica, sin depender de que cada log los agregue a mano.
app.Use(async (context, next) =>
{
    var tenantId = context.User.FindFirst("tenant_id")?.Value;
    var usuarioId = context.User.FindFirst("sub")?.Value;
    using (Serilog.Context.LogContext.PushProperty("TenantId", tenantId))
    using (Serilog.Context.LogContext.PushProperty("UsuarioId", usuarioId))
    {
        await next();
    }
});

app.MapControllers();

app.MapGet("/health", () => Results.Ok(new { status = "ok", timestamp = DateTime.UtcNow }))
   .AllowAnonymous();

app.Run();
