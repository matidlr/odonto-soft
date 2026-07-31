using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Odonto.Domain.Entities;
using Odonto.Infrastructure.Persistence;

namespace Odonto.Api.Controllers;

/// <summary>
/// Pacientes vistos/gestionados por el odontólogo o la recepción.
/// El alta pública (vía link, sin login) vive en PublicController; este
/// POST es para cuando el consultorio carga al paciente directamente
/// (por ejemplo, alguien que llama por teléfono o no usa el link solo).
/// </summary>
[ApiController]
[Route("api/pacientes")]
[Authorize(Policy = "TenantActivo")]
public class PacientesController : ControllerBase
{
    private readonly AppDbContext _db;

    public PacientesController(AppDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken ct)
    {
        var pacientes = await _db.Pacientes
            .Select(p => new { p.Id, p.Nombre, p.Dni, p.Telefono, p.Email, p.FechaNacimiento })
            .ToListAsync(ct);

        return Ok(pacientes);
    }

    public record CrearPacienteRequest(
        string Nombre,
        string? Dni,
        string? Telefono,
        string? Email,
        DateTime? FechaNacimiento);

    [HttpPost]
    public async Task<IActionResult> Crear(CrearPacienteRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Nombre))
            return BadRequest(new { message = "El nombre es obligatorio." });

        var tenantIdClaim = User.FindFirst("tenant_id")?.Value;
        if (!Guid.TryParse(tenantIdClaim, out var tenantId))
            return BadRequest(new { message = "No se pudo determinar el tenant del usuario." });

        var paciente = new Paciente
        {
            TenantId = tenantId,
            Nombre = request.Nombre,
            Dni = request.Dni,
            Telefono = request.Telefono,
            Email = request.Email,
            FechaNacimiento = request.FechaNacimiento
        };

        _db.Pacientes.Add(paciente);
        await _db.SaveChangesAsync(ct);

        return Ok(new { paciente.Id });
    }
}
