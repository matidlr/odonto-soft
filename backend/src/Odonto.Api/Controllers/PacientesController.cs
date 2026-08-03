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
    private readonly ILogger<PacientesController> _logger;

    public PacientesController(AppDbContext db, ILogger<PacientesController> logger)
    {
        _db = db;
        _logger = logger;
    }

    private Guid? UsuarioIdActual()
    {
        var claim = User.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)?.Value;
        return Guid.TryParse(claim, out var id) ? id : null;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] Guid? odontologoId,
        [FromQuery] bool incluirInactivos,
        CancellationToken ct)
    {
        var query = _db.Pacientes.AsQueryable();
        if (odontologoId is Guid oid) query = query.Where(p => p.OdontologoPrincipalId == oid);
        // Por default no mostramos los dados de baja, para no ensuciar el
        // listado normal; incluirInactivos=true los trae igual (por si
        // hace falta reactivar a alguien).
        if (!incluirInactivos) query = query.Where(p => p.Activo);

        var pacientes = await query
            .Select(p => new
            {
                p.Id,
                p.Nombre,
                p.Dni,
                p.Telefono,
                p.Email,
                p.FechaNacimiento,
                p.OdontologoPrincipalId,
                p.Activo
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

    /// <summary>
    /// Baja lógica: el paciente deja de aparecer en los listados, pero sus
    /// datos, turnos e historia clínica quedan intactos (nunca se borran).
    /// Se puede deshacer con /reactivar.
    /// </summary>
    [HttpDelete("{id}")]
    public async Task<IActionResult> Eliminar(Guid id, CancellationToken ct)
    {
        var paciente = await _db.Pacientes.FirstOrDefaultAsync(p => p.Id == id, ct);
        if (paciente is null) return NotFound();

        paciente.Activo = false;
        await _db.SaveChangesAsync(ct);

        _logger.LogWarning("Paciente {PacienteId} dado de baja por usuario {UsuarioId}",
            paciente.Id, UsuarioIdActual());

        return Ok(new { message = "Paciente dado de baja." });
    }

    [HttpPost("{id}/reactivar")]
    public async Task<IActionResult> Reactivar(Guid id, CancellationToken ct)
    {
        var paciente = await _db.Pacientes.FirstOrDefaultAsync(p => p.Id == id, ct);
        if (paciente is null) return NotFound();

        paciente.Activo = true;
        await _db.SaveChangesAsync(ct);

        _logger.LogWarning("Paciente {PacienteId} reactivado por usuario {UsuarioId}",
            paciente.Id, UsuarioIdActual());

        return Ok(new { message = "Paciente reactivado." });
    }
}
