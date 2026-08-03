using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Odonto.Application.Agenda;
using Odonto.Domain.Common;
using Odonto.Domain.Entities;
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

    /// <summary>Sede a usar cuando no se especifica una: la Principal del odontólogo.</summary>
    private async Task<Guid?> ResolverSedeId(Guid odontologoId, Guid? sedeIdPedido, CancellationToken ct)
    {
        if (sedeIdPedido is Guid pedida) return pedida;

        var principal = await _db.Sedes.FirstOrDefaultAsync(s => s.OdontologoId == odontologoId && s.EsPrincipal, ct);
        return principal?.Id;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] DateTime? desde,
        [FromQuery] DateTime? hasta,
        [FromQuery] Guid? odontologoId,
        [FromQuery] Guid? pacienteId,
        CancellationToken ct)
    {
        var query = _db.Turnos.AsQueryable();
        if (desde is DateTime d1) query = query.Where(t => t.FechaHora >= d1);
        if (hasta is DateTime d2) query = query.Where(t => t.FechaHora <= d2);
        if (odontologoId is Guid oid) query = query.Where(t => t.OdontologoId == oid);
        if (pacienteId is Guid pid) query = query.Where(t => t.PacienteId == pid);

        var turnos = await query
            .OrderBy(t => t.FechaHora)
            .Select(t => new
            {
                t.Id,
                t.OdontologoId,
                t.SedeId,
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

    public record VentanaResponse(string HoraInicio, string HoraFin);
    public record BloqueoResponse(Guid Id, string HoraInicio, string HoraFin);

    public record TurnoDelDiaResponse(
        Guid Id,
        string HoraInicio,
        string HoraFin,
        Guid PacienteId,
        string PacienteNombre,
        Guid? TipoTratamientoId,
        int DuracionMinutos,
        string Estado,
        bool OtraSede,
        string? SedeNombre);

    /// <summary>
    /// Vista de un día puntual para el calendario de la agenda: las
    /// ventanas habilitadas y los bloqueos (resueltos a partir de las
    /// reglas de Disponibilidad de la sede indicada) más los turnos ya
    /// reservados. Los turnos que devolvemos son de TODAS las sedes del
    /// odontólogo ese día (marcados con OtraSede) porque un turno en
    /// cualquier sede bloquea ese horario en todas las demás — nunca puede
    /// estar agendado dos veces a la misma hora.
    /// </summary>
    [HttpGet("dia")]
    public async Task<IActionResult> GetDia([FromQuery] Guid odontologoId, [FromQuery] DateTime fecha, [FromQuery] Guid? sedeId, CancellationToken ct)
    {
        var odontologo = await _db.Odontologos.FirstOrDefaultAsync(o => o.Id == odontologoId, ct);
        if (odontologo is null) return BadRequest(new { message = "Odontólogo inválido." });

        var sedeActualId = await ResolverSedeId(odontologoId, sedeId, ct);

        var fechaSolo = fecha.Date;
        var diaSemana = fechaSolo.DayOfWeek.ADiaSemana();

        var reglas = await _db.Disponibilidades
            .Where(d => d.OdontologoId == odontologoId && d.SedeId == sedeActualId &&
                ((d.Tipo == TipoDisponibilidad.Recurrente && d.DiaSemana == diaSemana) ||
                 (d.Tipo == TipoDisponibilidad.Excepcion && d.Fecha != null && d.Fecha.Value.Date == fechaSolo)))
            .ToListAsync(ct);

        var reglaTodoElDiaBloqueado = reglas
            .FirstOrDefault(r => r.Tipo == TipoDisponibilidad.Excepcion && r.Bloqueado && r.TodoElDia);

        var ventanas = reglas
            .Where(r => !r.Bloqueado && !r.TodoElDia && r.HoraInicio != null && r.HoraFin != null)
            .Select(r => new VentanaResponse(r.HoraInicio!.Value.ToString(@"hh\:mm"), r.HoraFin!.Value.ToString(@"hh\:mm")))
            .ToList();

        var bloqueos = reglas
            .Where(r => r.Bloqueado && !r.TodoElDia && r.HoraInicio != null && r.HoraFin != null)
            .Select(r => new BloqueoResponse(r.Id, r.HoraInicio!.Value.ToString(@"hh\:mm"), r.HoraFin!.Value.ToString(@"hh\:mm")))
            .ToList();

        // Ojo: a propósito NO filtramos por sede acá. Un turno en cualquier
        // sede del odontólogo ocupa ese horario en todas — así se refleja
        // en la grilla (y así se evita que se pueda reservar "por encima").
        var turnosDelDia = await _db.Turnos
            .Where(t => t.OdontologoId == odontologoId && t.FechaHora.Date == fechaSolo && t.Estado != TurnoEstado.Cancelado)
            .OrderBy(t => t.FechaHora)
            .Select(t => new
            {
                t.Id,
                t.FechaHora,
                t.SedeId,
                SedeNombre = t.Sede != null ? t.Sede.Nombre : null,
                t.PacienteId,
                PacienteNombre = t.Paciente.Nombre,
                t.TipoTratamientoId,
                t.DuracionMinutos,
                Estado = t.Estado.ToString()
            })
            .ToListAsync(ct);

        var turnos = turnosDelDia.Select(t => new TurnoDelDiaResponse(
            t.Id,
            t.FechaHora.ToString("HH:mm"),
            t.FechaHora.AddMinutes(t.DuracionMinutos).ToString("HH:mm"),
            t.PacienteId,
            t.PacienteNombre,
            t.TipoTratamientoId,
            t.DuracionMinutos,
            t.Estado,
            t.SedeId != sedeActualId,
            t.SedeNombre)).ToList();

        return Ok(new
        {
            fecha = fechaSolo.ToString("yyyy-MM-dd"),
            sedeId = sedeActualId,
            todoElDiaBloqueado = reglaTodoElDiaBloqueado is not null,
            todoElDiaBloqueadoId = reglaTodoElDiaBloqueado?.Id,
            ventanas,
            bloqueos,
            turnos
        });
    }

    public record ReservarTurnoManualRequest(
        Guid PacienteId,
        Guid OdontologoId,
        Guid? SedeId,
        Guid? TipoTratamientoId,
        DateTime FechaHora,
        // Si viene, pisa la duración del tipo de tratamiento (o el default
        // de 30 min): permite elegir "hasta qué hora" a mano en la agenda.
        int? DuracionMinutos);

    /// <summary>
    /// Reserva manual desde el consultorio (por ejemplo, un paciente que
    /// llama por teléfono, o alguien a quien le cuesta el autoservicio).
    /// Misma lógica que la reserva pública, pero sin pasar por el slug:
    /// el tenant sale del JWT y el filtro global ya restringe todo lo demás.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> Crear(ReservarTurnoManualRequest request, CancellationToken ct)
    {
        var paciente = await _db.Pacientes.FirstOrDefaultAsync(p => p.Id == request.PacienteId, ct);
        if (paciente is null) return BadRequest(new { message = "Paciente inválido." });

        var odontologo = await _db.Odontologos.FirstOrDefaultAsync(o => o.Id == request.OdontologoId, ct);
        if (odontologo is null) return BadRequest(new { message = "Odontólogo inválido." });

        var duracionMinutos = 30;
        if (request.TipoTratamientoId is Guid ttId)
        {
            var tipoTratamiento = await _db.TiposTratamiento.FirstOrDefaultAsync(t => t.Id == ttId, ct);
            if (tipoTratamiento is null) return BadRequest(new { message = "Tipo de tratamiento inválido." });
            duracionMinutos = tipoTratamiento.DuracionMinutos;
        }

        if (request.DuracionMinutos is int duracionManual)
        {
            if (duracionManual <= 0)
                return BadRequest(new { message = "La duración tiene que ser mayor a 0." });
            duracionMinutos = duracionManual;
        }

        // A propósito, este chequeo NUNCA filtra por sede: el odontólogo no
        // puede estar agendado a la misma hora en dos sedes distintas, así
        // que se compara contra todos sus turnos sin importar en cuál sede.
        var fin = request.FechaHora.AddMinutes(duracionMinutos);
        var haySolapamiento = await _db.Turnos.AnyAsync(t =>
            t.OdontologoId == request.OdontologoId &&
            t.Estado != TurnoEstado.Cancelado &&
            t.FechaHora < fin &&
            request.FechaHora < t.FechaHora.AddMinutes(t.DuracionMinutos), ct);

        if (haySolapamiento)
            return Conflict(new { message = "Ese horario ya no está disponible (el odontólogo ya tiene un turno a esa hora, en esta u otra sede)." });

        var tenantIdClaim = User.FindFirst("tenant_id")?.Value;
        if (!Guid.TryParse(tenantIdClaim, out var tenantId))
            return BadRequest(new { message = "No se pudo determinar el tenant del usuario." });

        var sedeId = await ResolverSedeId(request.OdontologoId, request.SedeId, ct);

        var turno = new Turno
        {
            TenantId = tenantId,
            OdontologoId = request.OdontologoId,
            SedeId = sedeId,
            PacienteId = request.PacienteId,
            TipoTratamientoId = request.TipoTratamientoId,
            FechaHora = request.FechaHora,
            DuracionMinutos = duracionMinutos,
            Estado = TurnoEstado.Solicitado
        };

        _db.Turnos.Add(turno);

        var notificaciones = RecordatorioScheduler.Generar(turno);
        _db.Notificaciones.AddRange(notificaciones);

        await _db.SaveChangesAsync(ct);

        return Ok(new
        {
            turno.Id,
            turno.FechaHora,
            turno.DuracionMinutos,
            Estado = turno.Estado.ToString(),
            recordatoriosProgramados = notificaciones.Count
        });
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
