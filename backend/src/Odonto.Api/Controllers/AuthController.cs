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
[AllowAnonymous]
[EnableRateLimiting("auth")]
public class AuthController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IConfiguration _configuration;
    private readonly IEmailSender _emailSender;
    private readonly ILogger<AuthController> _logger;
    private readonly PasswordHasher<Usuario> _passwordHasher = new();

    public AuthController(AppDbContext db, IConfiguration configuration, IEmailSender emailSender, ILogger<AuthController> logger)
    {
        _db = db;
        _configuration = configuration;
        _emailSender = emailSender;
        _logger = logger;
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
    public async Task<IActionResult> RegistrarOdontologo(RegistrarOdontologoRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Password) || request.Password.Length < 8)
            return BadRequest(new { message = "La contraseña debe tener al menos 8 caracteres." });

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

        return Ok(new { superAdmin.Email, message = "Contraseña actualizada." });
    }

    public record LoginRequest(string Email, string Password);

    [HttpPost("login")]
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

        var resultado = _passwordHasher.VerifyHashedPassword(usuario, usuario.PasswordHash, request.Password);
        if (resultado == PasswordVerificationResult.Failed)
        {
            _logger.LogWarning("Login fallido (contraseña incorrecta) para {Email} desde {IP}",
                request.Email, HttpContext.Connection.RemoteIpAddress);
            return Unauthorized(new { message = "Credenciales inválidas." });
        }

        var token = GenerarToken(usuario);
        return Ok(new { token, rol = usuario.Rol.ToString(), tenantId = usuario.TenantId });
    }

    public record OlvidePasswordRequest(string Email);

    [HttpPost("olvide-password")]
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

        return Ok(new { message = "Contraseña actualizada. Ya podés iniciar sesión." });
    }

    private string GenerarToken(Usuario usuario)
    {
        var jwtKey = _configuration["Jwt:Key"]!;
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, usuario.Id.ToString()),
            new("email", usuario.Email),
            new("rol", usuario.Rol.ToString())
        };

        if (usuario.TenantId is Guid tenantId)
        {
            claims.Add(new Claim("tenant_id", tenantId.ToString()));
        }

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _configuration["Jwt:Issuer"],
            audience: _configuration["Jwt:Audience"],
            claims: claims,
            expires: DateTime.UtcNow.AddHours(8),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
