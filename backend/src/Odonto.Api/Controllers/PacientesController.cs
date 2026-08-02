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
    public async Task<IActionResult> GetAll([FromQuery] Guid? odontologoId, CancellationToken ct)
    {
        var query = _db.Pacientes.AsQueryable();
        if (odontologoId is Guid oid) query = query.Where(p => p.OdontologoPrincipalId == oid);

        var pacientes = await query
            .Select(p => new
            {
                p.Id,
                p.Nombre,
                p.Dni,
                p.Telefono,
                p.Email,
                p.FechaNacimiento,
                p.OdontologoPrincipalId
            })
            .ToListAsync(ct);

        return Ok(pacientes);
    }

    public record CrearPacienteRequest(
        string Nombre,
        string? Dni,
        string? Telefono,
        string? Email,
        DateTime? FechaNacimiento,
        Guid? OdontologoPrincipalId);

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
            FechaNacimiento = request.FechaNacimiento,
            OdontologoPrincipalId = request.OdontologoPrincipalId
        };

        _db.Pacientes.Add(paciente);
        await _db.SaveChangesAsync(ct);

        return Ok(new { paciente.Id });
    }

    public record EditarPacienteRequest(
        string Nombre,
        string? Dni,
        string? Telefono,
        string? Email,
        DateTime? FechaNacimiento,
        Guid? OdontologoPrincipalId);

    [HttpPut("{id}")]
    public async Task<IActionResult> Editar(Guid id, EditarPacienteRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Nombre))
            return BadRequest(new { message = "El nombre es obligatorio." });

        var paciente = await _db.Pacientes.FirstOrDefaultAsync(p => p.Id == id, ct);
        if (paciente is null) return NotFound();

        paciente.Nombre = request.Nombre;
        paciente.Dni = request.Dni;
        paciente.Telefono = request.Telefono;
        paciente.Email = request.Email;
        paciente.FechaNacimiento = request.FechaNacimiento;
        paciente.OdontologoPrincipalId = request.OdontologoPrincipalId;

        await _db.SaveChangesAsync(ct);

        return Ok(new { paciente.Id });
    }
}
