using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Odonto.Api.Validacion;
using Odonto.Application.Common.Interfaces;
using Odonto.Domain.Common;
using Odonto.Domain.Entities;
using Odonto.Infrastructure.Persistence;

namespace Odonto.Api.Controllers;

// Todos los endpoints de este controller son sensibles a fuerza bruta o
// spam (login, alta de clínica, recuperación de contraseña): 5 pedidos
// por minuto por IP, ver Program.cs.
[ApiController]
[Route("api/auth")]
[EnableRateLimiting("auth")]
public class AuthController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IConfiguration _configuration;
    private readonly IEmailSender _emailSender;
    private readonly ILogger<AuthController> _logger;
    private readonly IWebHostEnvironment _environment;
    private readonly PasswordHasher<Usuario> _passwordHasher = new();

    public AuthController(
        AppDbContext db,
        IConfiguration configuration,
        IEmailSender emailSender,
        ILogger<AuthController> logger,
        IWebHostEnvironment environment)
    {
        _db = db;
        _configuration = configuration;
        _emailSender = emailSender;
        _logger = logger;
        _environment = environment;
    }

    public record RegistrarOdontologoRequest(
        string NombreClinica,
        string Slug,
        string NombreOdontologo,
        string Email,
        string Password,
        string Matricula,
        string? Especialidad);

    /// <summary>
    /// Alta de un odontólogo/clínica nueva. Crea el Tenant ya Activo (con
    /// un mes de prueba gratis), el Usuario (Rol=Owner) y su perfil de
    /// Odontologo. Pasado el mes, si no hay una suscripción de Mercado Pago
    /// pagando, el sistema suspende la cuenta solo (ver TenantEstadoService).
    /// </summary>
    [HttpPost("registrar-odontologo")]
    [AllowAnonymous]
    public async Task<IActionResult> RegistrarOdontologo(RegistrarOdontologoRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Password) || request.Password.Length < 8)
            return BadRequest(new { message = "La contraseña debe tener al menos 8 caracteres." });

        if (string.IsNullOrWhiteSpace(request.NombreClinica) || request.NombreClinica.Length > 200)
            return BadRequest(new { message = "El nombre de la clínica es obligatorio y no puede superar los 200 caracteres." });

        if (string.IsNullOrWhiteSpace(request.NombreOdontologo) || request.NombreOdontologo.Length > 200)
            return BadRequest(new { message = "El nombre del odontólogo es obligatorio y no puede superar los 200 caracteres." });

        if (string.IsNullOrWhiteSpace(request.Matricula) || request.Matricula.Length > 50)
            return BadRequest(new { message = "La matrícula es obligatoria y no puede superar los 50 caracteres." });

        if (!Validaciones.EsEmailValido(request.Email))
            return BadRequest(new { message = "El email no tiene un formato válido." });

        // El slug queda expuesto en la URL pública (/r/{slug}), así que se
        // restringe a algo que no rompa el routing ni links compartidos:
        // minúsculas, números y guiones medios, sin espacios ni símbolos.
        if (string.IsNullOrWhiteSpace(request.Slug) ||
            request.Slug.Length > 60 ||
            !System.Text.RegularExpressions.Regex.IsMatch(request.Slug, "^[a-z0-9]+(-[a-z0-9]+)*$"))
        {
            return BadRequest(new { message = "El slug solo puede tener minúsculas, números y guiones medios (ej: clinica-sonrisas)." });
        }

        if (await _db.Tenants.AnyAsync(t => t.Slug == request.Slug, ct))
            return Conflict(new { message = "Ese slug ya está en uso." });

        // IgnoreQueryFilters: acá todavía no hay tenant en contexto (request anónimo),
        // así que el filtro global lo restringiría a "sin tenant" si no lo salteamos.
        if (await _db.Usuarios.IgnoreQueryFilters().AnyAsync(u => u.Email == request.Email, ct))
            return Conflict(new { message = "Ese email ya está registrado." });

        // Toda clínica nueva arranca en el plan más económico (el primero
        // por Orden); el SuperAdmin puede subirla de plan después.
        var planPorDefecto = await _db.Planes
            .Where(p => p.Activo)
            .OrderBy(p => p.Orden)
            .FirstOrDefaultAsync(ct);

        var tenant = new Tenant
        {
            Nombre = request.NombreClinica,
            Slug = request.Slug,
            // Arranca activa de una: tiene un mes gratis, no hace falta que
            // nadie la active a mano. Si el mes se vence sin pago, el
            // sistema mismo la pasa a Suspendido (ver TenantEstadoService).
            Estado = TenantEstado.Activo,
            FechaFinPrueba = DateTime.UtcNow.AddMonths(1),
            PlanId = planPorDefecto?.Id
        };

        var usuario = new Usuario
        {
            TenantId = tenant.Id,
            Nombre = request.NombreOdontologo,
            Email = request.Email,
            Rol = Rol.Owner,
            EstaActivo = true
        };
        usuario.PasswordHash = _passwordHasher.HashPassword(usuario, request.Password);

        var odontologo = new Odontologo
        {
            TenantId = tenant.Id,
            UsuarioId = usuario.Id,
            Nombre = request.NombreOdontologo,
            Matricula = request.Matricula,
            Especialidad = request.Especialidad
        };

        _db.Tenants.Add(tenant);
        _db.Usuarios.Add(usuario);
        _db.Odontologos.Add(odontologo);

        // Todo odontólogo arranca con una sede Principal (su consultorio),
        // para poder cargar horarios y turnos desde el primer momento. Si
        // más adelante trabaja en otro lugar, agrega una sede adicional.
        _db.Sedes.Add(new Sede
        {
            TenantId = tenant.Id,
            OdontologoId = odontologo.Id,
            Nombre = "Principal",
            EsPrincipal = true
        });

        await _db.SaveChangesAsync(ct);

        return Ok(new { tenantId = tenant.Id, estado = tenant.Estado.ToString() });
    }

    public record BootstrapSuperAdminRequest(string Email, string Password, string BootstrapKey);

    /// <summary>
    /// Crea el usuario SuperAdmin (vos, el dueño de la plataforma). Pensado para
    /// usarse una sola vez, protegido por una clave que solo vos conocés
    /// (configurada en appsettings como Bootstrap:Key). No aparece en ningún
    /// link público ni lo va a usar un odontólogo.
    /// </summary>
    [HttpPost("bootstrap-superadmin")]
    [AllowAnonymous]
    public async Task<IActionResult> BootstrapSuperAdmin(BootstrapSuperAdminRequest request, CancellationToken ct)
    {
        var expectedKey = _configuration["Bootstrap:Key"];
        if (string.IsNullOrEmpty(expectedKey) || request.BootstrapKey != expectedKey)
            return Unauthorized(new { message = "Clave de bootstrap inválida." });

        if (await _db.Usuarios.IgnoreQueryFilters().AnyAsync(u => u.Rol == Rol.SuperAdmin, ct))
            return Conflict(new { message = "Ya existe un SuperAdmin. Este endpoint es solo para la creación inicial." });

        var usuario = new Usuario
        {
            TenantId = null,
            Email = request.Email,
            Rol = Rol.SuperAdmin,
            EstaActivo = true
        };
        usuario.PasswordHash = _passwordHasher.HashPassword(usuario, request.Password);

        _db.Usuarios.Add(usuario);
        await _db.SaveChangesAsync(ct);

        return Ok(new { usuario.Id, message = "SuperAdmin creado. Iniciá sesión con /api/auth/login." });
    }

    public record ResetSuperAdminPasswordRequest(string NewPassword, string BootstrapKey);

    /// <summary>
    /// Recupera el acceso al SuperAdmin si se te perdió la contraseña.
    /// Protegido con la misma Bootstrap:Key (solo vos la tenés). Busca al
    /// único usuario con Rol=SuperAdmin y le resetea la contraseña.
    /// </summary>
    [HttpPost("reset-superadmin-password")]
    [AllowAnonymous]
    public async Task<IActionResult> ResetSuperAdminPassword(ResetSuperAdminPasswordRequest request, CancellationToken ct)
    {
        var expectedKey = _configuration["Bootstrap:Key"];
        if (string.IsNullOrEmpty(expectedKey) || request.BootstrapKey != expectedKey)
            return Unauthorized(new { message = "Clave de bootstrap inválida." });

        if (string.IsNullOrWhiteSpace(request.NewPassword) || request.NewPassword.Length < 8)
            return BadRequest(new { message = "La contraseña debe tener al menos 8 caracteres." });

        var superAdmin = await _db.Usuarios.IgnoreQueryFilters().FirstOrDefaultAsync(u => u.Rol == Rol.SuperAdmin, ct);
        if (superAdmin is null)
            return NotFound(new { message = "No existe ningún SuperAdmin todavía. Usá /bootstrap-superadmin." });

        superAdmin.PasswordHash = _passwordHasher.HashPassword(superAdmin, request.NewPassword);
        await _db.SaveChangesAsync(ct);

        _logger.LogWarning("Contraseña de SuperAdmin reseteada (vía bootstrap key) para {Email} desde {IP}",
            superAdmin.Email, HttpContext.Connection.RemoteIpAddress);

        return Ok(new { superAdmin.Email, message = "Contraseña actualizada." });
    }

    public record LoginRequest(string Email, string Password);

    // Tras esta cantidad de intentos fallidos seguidos, la cuenta queda
    // bloqueada por un rato (más allá del rate-limit por IP, que no protege
    // si el ataque viene rotando de IP).
    private const int MaxIntentosFallidos = 5;
    private static readonly TimeSpan DuracionBloqueo = TimeSpan.FromMinutes(15);

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login(LoginRequest request, CancellationToken ct)
    {
        var usuario = await _db.Usuarios
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.Email == request.Email, ct);

        if (usuario is null || !usuario.EstaActivo)
        {
            _logger.LogWarning("Login fallido (usuario inexistente o inactivo) para {Email} desde {IP}",
                request.Email, HttpContext.Connection.RemoteIpAddress);
            return Unauthorized(new { message = "Credenciales inválidas." });
        }

        if (usuario.BloqueadoHasta is DateTime bloqueadoHasta && bloqueadoHasta > DateTime.UtcNow)
        {
            _logger.LogWarning("Login rechazado (cuenta bloqueada) para {Email} desde {IP}",
                request.Email, HttpContext.Connection.RemoteIpAddress);
            return StatusCode(StatusCodes.Status423Locked, new
            {
                message = "Esta cuenta está bloqueada temporalmente por varios intentos fallidos. Probá de nuevo en unos minutos."
            });
        }

        var resultado = _passwordHasher.VerifyHashedPassword(usuario, usuario.PasswordHash, request.Password);
        if (resultado == PasswordVerificationResult.Failed)
        {
            usuario.IntentosFallidos++;
            if (usuario.IntentosFallidos >= MaxIntentosFallidos)
            {
                usuario.BloqueadoHasta = DateTime.UtcNow.Add(DuracionBloqueo);
                usuario.IntentosFallidos = 0;
                _logger.LogWarning("Cuenta bloqueada por {Minutos} minutos tras intentos fallidos repetidos: {Email} desde {IP}",
                    DuracionBloqueo.TotalMinutes, request.Email, HttpContext.Connection.RemoteIpAddress);
            }
            await _db.SaveChangesAsync(ct);

            _logger.LogWarning("Login fallido (contraseña incorrecta) para {Email} desde {IP}",
                request.Email, HttpContext.Connection.RemoteIpAddress);
            return Unauthorized(new { message = "Credenciales inválidas." });
        }

        usuario.IntentosFallidos = 0;
        usuario.BloqueadoHasta = null;
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Login exitoso para {Email} desde {IP}",
            request.Email, HttpContext.Connection.RemoteIpAddress);

        var token = GenerarToken(usuario);
        await EmitirRefreshTokenAsync(usuario, ct);
        return Ok(new { token, rol = usuario.Rol.ToString(), tenantId = usuario.TenantId });
    }

    /// <summary>
    /// Cambia el access token (JWT) por uno nuevo usando el refresh token
    /// guardado en la cookie httpOnly. El frontend llama esto solo cuando un
    /// pedido normal le devuelve 401 (el access token de 20 minutos venció),
    /// así el usuario no tiene que volver a loguearse cada rato.
    /// </summary>
    [HttpPost("refresh")]
    [AllowAnonymous]
    public async Task<IActionResult> Refresh(CancellationToken ct)
    {
        var cookieToken = Request.Cookies[RefreshCookieName];
        if (string.IsNullOrEmpty(cookieToken))
            return Unauthorized(new { message = "No hay sesión para renovar." });

        var hash = HashToken(cookieToken);
        var refreshToken = await _db.RefreshTokens
            .Include(r => r.Usuario)
            .FirstOrDefaultAsync(r => r.TokenHash == hash, ct);

        if (refreshToken is null || refreshToken.Revocado || refreshToken.FechaExpiracion < DateTime.UtcNow
            || refreshToken.Usuario is null || !refreshToken.Usuario.EstaActivo)
        {
            Response.Cookies.Delete(RefreshCookieName, CookieOptionsBase());
            return Unauthorized(new { message = "La sesión venció. Iniciá sesión de nuevo." });
        }

        // Rotación: el refresh token usado queda revocado y se emite uno
        // nuevo. Si alguien roba un refresh token viejo y lo intenta usar
        // después de que el dueño ya lo rotó, esto lo detectaría (quedaría
        // marcado Revocado), aunque hoy no cortamos toda la sesión ante eso.
        refreshToken.Revocado = true;
        refreshToken.FechaRevocado = DateTime.UtcNow;

        var usuario = refreshToken.Usuario;
        var nuevoToken = GenerarToken(usuario);
        await EmitirRefreshTokenAsync(usuario, ct);

        return Ok(new { token = nuevoToken, rol = usuario.Rol.ToString(), tenantId = usuario.TenantId });
    }

    /// <summary>Cierra la sesión actual: revoca el refresh token de esta cookie nada más.</summary>
    [HttpPost("logout")]
    [AllowAnonymous]
    public async Task<IActionResult> Logout(CancellationToken ct)
    {
        var cookieToken = Request.Cookies[RefreshCookieName];
        if (!string.IsNullOrEmpty(cookieToken))
        {
            var hash = HashToken(cookieToken);
            var refreshToken = await _db.RefreshTokens.FirstOrDefaultAsync(r => r.TokenHash == hash, ct);
            if (refreshToken is not null && !refreshToken.Revocado)
            {
                refreshToken.Revocado = true;
                refreshToken.FechaRevocado = DateTime.UtcNow;
                await _db.SaveChangesAsync(ct);
            }
        }

        Response.Cookies.Delete(RefreshCookieName, CookieOptionsBase());
        return Ok(new { message = "Sesión cerrada." });
    }

    /// <summary>
    /// Cierra la sesión en todos los dispositivos: revoca todos los refresh
    /// tokens del usuario logueado (no solo el de esta cookie). Útil si
    /// sospechás que alguien más tiene acceso a tu cuenta.
    /// </summary>
    [HttpPost("logout-todos")]
    [Authorize]
    public async Task<IActionResult> LogoutTodos(CancellationToken ct)
    {
        var usuarioIdClaim = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        if (!Guid.TryParse(usuarioIdClaim, out var usuarioId))
            return Unauthorized();

        var tokens = await _db.RefreshTokens
            .Where(r => r.UsuarioId == usuarioId && !r.Revocado)
            .ToListAsync(ct);

        foreach (var t in tokens)
        {
            t.Revocado = true;
            t.FechaRevocado = DateTime.UtcNow;
        }
        await _db.SaveChangesAsync(ct);

        _logger.LogWarning("Cierre de sesión en todos los dispositivos para usuario {UsuarioId} desde {IP}",
            usuarioId, HttpContext.Connection.RemoteIpAddress);

        Response.Cookies.Delete(RefreshCookieName, CookieOptionsBase());
        return Ok(new { message = $"Se cerraron {tokens.Count} sesión(es) activa(s)." });
    }

    public record OlvidePasswordRequest(string Email);

    [HttpPost("olvide-password")]
    [AllowAnonymous]
    public async Task<IActionResult> OlvidePassword(OlvidePasswordRequest request, CancellationToken ct)
    {
        // Siempre devolvemos el mismo mensaje genérico, exista o no el email,
        // para no filtrar qué correos están registrados.
        var respuestaGenerica = Ok(new { message = "Si el email existe, te enviamos un enlace para restablecer la contraseña." });

        var usuario = await _db.Usuarios
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.Email == request.Email, ct);

        if (usuario is null || !usuario.EstaActivo)
            return respuestaGenerica;

        var tokenBytes = RandomNumberGenerator.GetBytes(32);
        var token = Convert.ToBase64String(tokenBytes)
            .Replace("+", "-").Replace("/", "_").Replace("=", "");

        _db.TokensResetPassword.Add(new TokenResetPassword
        {
            UsuarioId = usuario.Id,
            Token = token,
            FechaExpiracion = DateTime.UtcNow.AddHours(1)
        });
        await _db.SaveChangesAsync(ct);

        _logger.LogWarning("Pedido de reseteo de contraseña para {Email} desde {IP}",
            usuario.Email, HttpContext.Connection.RemoteIpAddress);

        var frontendUrl = _configuration["Cors:AllowedOrigin"] ?? "http://localhost:4200";
        var link = $"{frontendUrl}/resetear-password?token={token}";
        var html = $@"
            <p>Recibimos un pedido para restablecer tu contraseña.</p>
            <p><a href=""{link}"">Hacé clic acá para elegir una nueva contraseña</a></p>
            <p>Este enlace vence en 1 hora. Si no fuiste vos, podés ignorar este email.</p>";

        await _emailSender.EnviarAsync(usuario.Email, null, "Restablecer tu contraseña", html, ct);

        return respuestaGenerica;
    }

    public record ResetearPasswordRequest(string Token, string NewPassword);

    [HttpPost("resetear-password")]
    [AllowAnonymous]
    public async Task<IActionResult> ResetearPassword(ResetearPasswordRequest request, CancellationToken ct)
    {
        // No confiamos solo en la validación del frontend: esto se puede
        // llamar directo a la API salteándola.
        if (string.IsNullOrWhiteSpace(request.NewPassword) || request.NewPassword.Length < 8)
            return BadRequest(new { message = "La contraseña debe tener al menos 8 caracteres." });

        var tokenRow = await _db.TokensResetPassword
            .FirstOrDefaultAsync(t => t.Token == request.Token, ct);

        if (tokenRow is null || tokenRow.Usado || tokenRow.FechaExpiracion < DateTime.UtcNow)
            return BadRequest(new { message = "El enlace es inválido o venció. Pedí uno nuevo." });

        var usuario = await _db.Usuarios
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.Id == tokenRow.UsuarioId, ct);

        if (usuario is null)
            return BadRequest(new { message = "El enlace es inválido o venció. Pedí uno nuevo." });

        usuario.PasswordHash = _passwordHasher.HashPassword(usuario, request.NewPassword);
        tokenRow.Usado = true;
        await _db.SaveChangesAsync(ct);

        _logger.LogWarning("Contraseña cambiada (vía link de reseteo) para {Email} desde {IP}",
            usuario.Email, HttpContext.Connection.RemoteIpAddress);

        return Ok(new { message = "Contraseña actualizada. Ya podés iniciar sesión." });
    }

    private string GenerarToken(Usuario usuario)
    {
        var jwtKey = _configuration["Jwt:Key"]!;
        // A propósito, el JWT lleva solo lo mínimo para identificar y
        // autorizar al usuario (UserId, ClinicaId, Rol). Un JWT no está
        // cifrado, solo firmado — cualquiera con el token puede leer su
        // contenido en texto plano (jwt.io, por ejemplo), así que no va
        // acá nada de email ni otro dato personal. Si algún endpoint
        // necesita el email, lo busca en la base con el UsuarioId.
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, usuario.Id.ToString()),
            new("rol", usuario.Rol.ToString())
        };

        if (usuario.TenantId is Guid tenantId)
        {
            claims.Add(new Claim("tenant_id", tenantId.ToString()));
        }

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        // Access token de vida corta a propósito: si se filtra (XSS, log,
        // etc.) la ventana de uso es chica. La sesión "larga" la sostiene el
        // refresh token, que vive aparte en una cookie httpOnly.
        var token = new JwtSecurityToken(
            issuer: _configuration["Jwt:Issuer"],
            audience: _configuration["Jwt:Audience"],
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(20),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private const string RefreshCookieName = "odonto_refresh";
    private static readonly TimeSpan DuracionRefreshToken = TimeSpan.FromDays(30);

    private async Task EmitirRefreshTokenAsync(Usuario usuario, CancellationToken ct)
    {
        var tokenBytes = RandomNumberGenerator.GetBytes(32);
        var tokenPlano = Convert.ToBase64String(tokenBytes)
            .Replace("+", "-").Replace("/", "_").Replace("=", "");

        _db.RefreshTokens.Add(new RefreshToken
        {
            UsuarioId = usuario.Id,
            TokenHash = HashToken(tokenPlano),
            FechaExpiracion = DateTime.UtcNow.Add(DuracionRefreshToken)
        });
        await _db.SaveChangesAsync(ct);

        var options = CookieOptionsBase();
        options.Expires = DateTimeOffset.UtcNow.Add(DuracionRefreshToken);
        Response.Cookies.Append(RefreshCookieName, tokenPlano, options);
    }

    // No guardamos el refresh token en texto plano en la base: si alguien
    // accediera a la base de datos, no podría usarlo directamente.
    private static string HashToken(string tokenPlano)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(tokenPlano));
        return Convert.ToHexString(bytes);
    }

    // Secure=true exige HTTPS, así que en Development (Kestrel solo escucha
    // por http en local) lo desactivamos para poder probar; en cualquier
    // otro ambiente va con Secure=true siempre.
    // TODO producción: si el día de mañana front y back quedan en
    // subdominios distintos (app.tudominio.com / api.tudominio.com), hay
    // que agregar Domain=".tudominio.com" acá para que la cookie viaje
    // entre los dos. Hoy, en localhost con distinto puerto, no hace falta.
    //
    // SameSite=Strict (protección CSRF): esta cookie solo la manda el
    // navegador en pedidos que salen del propio frontend (nuestro fetch a
    // /api/auth/refresh, /logout, /logout-todos). Nunca hace falta que
    // viaje en una navegación de otro sitio (no es un link ni un botón
    // "Volver"), así que Strict no rompe nada acá y cierra la puerta a que
    // un sitio malicioso la haga viajar de arrastre.
    private CookieOptions CookieOptionsBase() => new()
    {
        HttpOnly = true,
        Secure = !_environment.IsDevelopment(),
        SameSite = SameSiteMode.Strict,
        Path = "/api/auth"
    };
}
