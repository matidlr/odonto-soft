using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Odonto.Domain.Common;
using Odonto.Domain.Entities;
using Odonto.Infrastructure.Persistence;

namespace Odonto.Api.Controllers;

/// <summary>
/// Endpoints públicos (sin login) que usa la página de registro de pacientes:
/// /r/{slug} en el frontend consume esto para mostrar los datos de la clínica
/// y mandar el formulario de alta del paciente.
/// Solo funciona si el tenant está Activo (no tiene sentido dejar que se
/// registren pacientes en una clínica que no pagó o está suspendida).
/// </summary>
[ApiController]
[Route("api/public")]
[AllowAnonymous]
public class PublicController : ControllerBase
{
    private readonly AppDbContext _db;

    public PublicController(AppDbContext db)
    {
        _db = db;
    }

    [HttpGet("clinicas/{slug}")]
    public async Task<IActionResult> GetClinica(string slug, CancellationToken ct)
    {
        var tenant = await _db.Tenants
            .Where(t => t.Slug == slug && t.Estado == TenantEstado.Activo)
            .Select(t => new { t.Nombre, t.Slug })
            .FirstOrDefaultAsync(ct);

        return tenant is null
            ? NotFound(new { message = "Clínica no encontrada o no disponible." })
            : Ok(tenant);
    }

    public record RegistrarPacienteRequest(
        string Nombre,
        string? Dni,
        string? Telefono,
        string? Email,
        DateTime? FechaNacimiento);

    [HttpPost("clinicas/{slug}/pacientes")]
    public async Task<IActionResult> RegistrarPaciente(string slug, RegistrarPacienteRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Nombre))
            return BadRequest(new { message = "El nombre es obligatorio." });

        var tenant = await _db.Tenants
            .FirstOrDefaultAsync(t => t.Slug == slug && t.Estado == TenantEstado.Activo, ct);

        if (tenant is null)
            return NotFound(new { message = "Clínica no encontrada o no disponible." });

        var paciente = new Paciente
        {
            TenantId = tenant.Id,
            Nombre = request.Nombre,
            Dni = request.Dni,
            Telefono = request.Telefono,
            Email = request.Email,
            FechaNacimiento = request.FechaNacimiento
        };

        _db.Pacientes.Add(paciente);
        await _db.SaveChangesAsync(ct);

        return Ok(new { paciente.Id, message = "Registro exitoso." });
    }
}
