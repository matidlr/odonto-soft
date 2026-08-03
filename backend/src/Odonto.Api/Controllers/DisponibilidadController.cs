using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Odonto.Domain.Common;
using Odonto.Domain.Entities;
using Odonto.Infrastructure.Persistence;

namespace Odonto.Api.Controllers;

/// <summary>
/// Reglas de disponibilidad del odontólogo: patrón semanal recurrente
/// (ej. "todos los lunes de 9 a 13") y excepciones puntuales (bloquear un
/// día, o abrir un hueco extra fuera del patrón habitual).
/// </summary>
[ApiController]
[Route("api/disponibilidad")]
[Authorize(Policy = "TenantActivo")]
public class DisponibilidadController : ControllerBase
{
    private readonly AppDbContext _db;

    public DisponibilidadController(AppDbContext db)
    {
        _db = db;
    }

    /// <summary>Sede a usar cuando no se especifica una: la Principal del odontólogo.</summary>
    private async Task<Guid?> ResolverSedeId(Guid odontologoId, Guid? sedeIdPedido, CancellationToken ct)
    {
        if (sedeIdPedido is Guid pedida) return pedida;

        var principal = await _db.Sedes.FirstOrDefaultAsync(s => s.OdontologoId == odontologoId && s.EsPrincipal, ct);
        return principal?.Id;
    }

    public record CrearDisponibilidadRequest(
        Guid OdontologoId,
        Guid? SedeId,
        TipoDisponibilidad Tipo,
        DiaSemana? DiaSemana,
        DateTime? Fecha,
        bool TodoElDia,
        TimeSpan? HoraInicio,
        TimeSpan? HoraFin,
        bool Bloqueado);

    [HttpPost]
    public async Task<IActionResult> Crear(CrearDisponibilidadRequest request, CancellationToken ct)
    {
        var odontologo = await _db.Odontologos.FirstOrDefaultAsync(o => o.Id == request.OdontologoId, ct);
        if (odontologo is null)
            return BadRequest(new { message = "Odontólogo inválido." });

        if (request.Tipo == TipoDisponibilidad.Recurrente && request.DiaSemana is null)
            return BadRequest(new { message = "Falta DiaSemana para una regla recurrente." });

        if (request.Tipo == TipoDisponibilidad.Excepcion && request.Fecha is null)
            return BadRequest(new { message = "Falta Fecha para una excepción." });

        if (!request.TodoElDia && (request.HoraInicio is null || request.HoraFin is null))
            return BadRequest(new { message = "Falta HoraInicio/HoraFin (o marcá TodoElDia=true)." });

        var sedeId = await ResolverSedeId(request.OdontologoId, request.SedeId, ct);

        var disponibilidad = new Disponibilidad
        {
            TenantId = odontologo.TenantId,
            OdontologoId = odontologo.Id,
            SedeId = sedeId,
            Tipo = request.Tipo,
            DiaSemana = request.DiaSemana,
            Fecha = request.Fecha,
            TodoElDia = request.TodoElDia,
            HoraInicio = request.HoraInicio,
            HoraFin = request.HoraFin,
            Bloqueado = request.Bloqueado
        };

        _db.Disponibilidades.Add(disponibilidad);
        await _db.SaveChangesAsync(ct);

        return Ok(new { disponibilidad.Id });
    }

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] Guid? odontologoId, [FromQuery] Guid? sedeId, CancellationToken ct)
    {
        var query = _db.Disponibilidades.AsQueryable();
        if (odontologoId is Guid id) query = query.Where(d => d.OdontologoId == id);
        if (sedeId is Guid sid) query = query.Where(d => d.SedeId == sid);

        var resultado = await query
            .Select(d => new
            {
                d.Id,
                d.OdontologoId,
                d.SedeId,
                Tipo = d.Tipo.ToString(),
                DiaSemana = d.DiaSemana == null ? null : d.DiaSemana.ToString(),
                d.Fecha,
                d.TodoElDia,
                d.HoraInicio,
                d.HoraFin,
                d.Bloqueado
            })
            .ToListAsync(ct);

        return Ok(resultado);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Eliminar(Guid id, CancellationToken ct)
    {
        var disponibilidad = await _db.Disponibilidades.FindAsync(new object[] { id }, ct);
        if (disponibilidad is null) return NotFound();

        _db.Disponibilidades.Remove(disponibilidad);
        await _db.SaveChangesAsync(ct);

        return NoContent();
    }
}
