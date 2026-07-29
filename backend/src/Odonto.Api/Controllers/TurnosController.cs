using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Odonto.Domain.Common;
using Odonto.Infrastructure.Persistence;

namespace Odonto.Api.Controllers;

/// <summary>
/// Agenda propia del odontólogo/clínica: ver los turnos y cambiar su estado
/// (confirmar, cancelar, marcar completado o ausente).
/// </summary>
[ApiController]
[Route("api/turnos")]
[Authorize(Policy = "TenantActivo")]
public class TurnosController : ControllerBase
{
    private readonly AppDbContext _db;

    public TurnosController(AppDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] DateTime? desde,
        [FromQuery] DateTime? hasta,
        [FromQuery] Guid? odontologoId,
        CancellationToken ct)
    {
        var query = _db.Turnos.AsQueryable();
        if (desde is DateTime d1) query = query.Where(t => t.FechaHora >= d1);
        if (hasta is DateTime d2) query = query.Where(t => t.FechaHora <= d2);
        if (odontologoId is Guid oid) query = query.Where(t => t.OdontologoId == oid);

        var turnos = await query
            .OrderBy(t => t.FechaHora)
            .Select(t => new
            {
                t.Id,
                t.OdontologoId,
                t.PacienteId,
                PacienteNombre = t.Paciente.Nombre,
                t.TipoTratamientoId,
                t.FechaHora,
                t.DuracionMinutos,
                Estado = t.Estado.ToString()
            })
            .ToListAsync(ct);

        return Ok(turnos);
    }

    public record CambiarEstadoRequest(TurnoEstado Estado);

    [HttpPut("{id}/estado")]
    public async Task<IActionResult> CambiarEstado(Guid id, CambiarEstadoRequest request, CancellationToken ct)
    {
        var turno = await _db.Turnos.FindAsync(new object[] { id }, ct);
        if (turno is null) return NotFound();

        turno.Estado = request.Estado;
        await _db.SaveChangesAsync(ct);

        return Ok(new { turno.Id, Estado = turno.Estado.ToString() });
    }
}
