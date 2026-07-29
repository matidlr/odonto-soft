using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Odonto.Domain.Common;
using Odonto.Domain.Entities;
using Odonto.Infrastructure.Persistence;

namespace Odonto.Api.Controllers;

[ApiController]
[Route("api/auth")]
[AllowAnonymous]
public class AuthController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IConfiguration _configuration;
    private readonly PasswordHasher<Usuario> _passwordHasher = new();

    public AuthController(AppDbContext db, IConfiguration configuration)
    {
        _db = db;
        _configuration = configuration;
    }

    public record RegistrarOdontologoRequest(
        string NombreClinica,
        string Slug,
        string Email,
        string Password,
        string Matricula,
        string? Especialidad);

    /// <summary>
    /// Alta de un odontólogo/clínica nueva. Crea el Tenant en estado
    /// PendienteDeActivacion, el Usuario (Rol=Owner) y su perfil de Odontologo.
    /// La activación real (Mercado Pago / SuperAdmin) viene en un paso futuro.
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

        var tenant = new Tenant
        {
            Nombre = request.NombreClinica,
            Slug = request.Slug,
            Estado = TenantEstado.PendienteDeActivacion
        };

        var usuario = new Usuario
        {
            TenantId = tenant.Id,
            Email = request.Email,
            Rol = Rol.Owner,
            EstaActivo = true
        };
        usuario.PasswordHash = _passwordHasher.HashPassword(usuario, request.Password);

        var odontologo = new Odontologo
        {
            TenantId = tenant.Id,
            UsuarioId = usuario.Id,
            Matricula = request.Matricula,
            Especialidad = request.Especialidad
        };

        _db.Tenants.Add(tenant);
        _db.Usuarios.Add(usuario);
        _db.Odontologos.Add(odontologo);
        await _db.SaveChangesAsync(ct);

        return Ok(new { tenantId = tenant.Id, estado = tenant.Estado.ToString() });
    }

    public record LoginRequest(string Email, string Password);

    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginRequest request, CancellationToken ct)
    {
        var usuario = await _db.Usuarios
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.Email == request.Email, ct);

        if (usuario is null || !usuario.EstaActivo)
            return Unauthorized(new { message = "Credenciales inválidas." });

        var resultado = _passwordHasher.VerifyHashedPassword(usuario, usuario.PasswordHash, request.Password);
        if (resultado == PasswordVerificationResult.Failed)
            return Unauthorized(new { message = "Credenciales inválidas." });

        var token = GenerarToken(usuario);
        return Ok(new { token, rol = usuario.Rol.ToString(), tenantId = usuario.TenantId });
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
