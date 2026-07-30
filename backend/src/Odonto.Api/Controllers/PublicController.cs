using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Odonto.Application.Agenda;
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

    [HttpGet("clinicas/{slug}/odontologos")]
    public async Task<IActionResult> GetOdontologos(string slug, CancellationToken ct)
    {
        var tenant = await _db.Tenants.FirstOrDefaultAsync(t => t.Slug == slug && t.Estado == TenantEstado.Activo, ct);
        if (tenant is null) return NotFound(new { message = "Clínica no encontrada o no disponible." });

        var odontologos = await _db.Odontologos
            .IgnoreQueryFilters()
            .Where(o => o.TenantId == tenant.Id)
            .Select(o => new { o.Id, Nombre = o.Usuario.Nombre, o.Especialidad })
            .ToListAsync(ct);

        return Ok(odontologos);
    }

    [HttpGet("clinicas/{slug}/odontologos/{odontologoId}/horarios-disponibles")]
    public async Task<IActionResult> GetHorariosDisponibles(
        string slug,
        Guid odontologoId,
        [FromQuery] DateTime fecha,
        [FromQuery] Guid? tipoTratamientoId,
        CancellationToken ct)
    {
        var tenant = await _db.Tenants.FirstOrDefaultAsync(t => t.Slug == slug && t.Estado == TenantEstado.Activo, ct);
        if (tenant is null) return NotFound(new { message = "Clínica no encontrada o no disponible." });

        var odontologo = await _db.Odontologos
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(o => o.Id == odontologoId && o.TenantId == tenant.Id, ct);
        if (odontologo is null) return NotFound(new { message = "Odontólogo no encontrado." });

        var duracionMinutos = 30;
        if (tipoTratamientoId is Guid ttId)
        {
            var tipo = await _db.TiposTratamiento
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(t => t.Id == ttId && t.TenantId == tenant.Id, ct);
            if (tipo is not null) duracionMinutos = tipo.DuracionMinutos;
        }

        var fechaSolo = fecha.Date;
        var diaSemana = fechaSolo.DayOfWeek.ADiaSemana();

        var reglas = await _db.Disponibilidades
            .IgnoreQueryFilters()
            .Where(d => d.OdontologoId == odontologoId &&
                ((d.Tipo == TipoDisponibilidad.Recurrente && d.DiaSemana == diaSemana) ||
                 (d.Tipo == TipoDisponibilidad.Excepcion && d.Fecha != null && d.Fecha.Value.Date == fechaSolo)))
            .ToListAsync(ct);

        // Si hay una excepción de "todo el día" bloqueada para esta fecha, no hay nada disponible.
        if (reglas.Any(r => r.Tipo == TipoDisponibilidad.Excepcion && r.Bloqueado && r.TodoElDia))
        {
            return Ok(new { fecha = fechaSolo.ToString("yyyy-MM-dd"), duracionMinutos, slots = Array.Empty<string>() });
        }

        var habilitadas = reglas
            .Where(r => !r.Bloqueado && !r.TodoElDia && r.HoraInicio != null && r.HoraFin != null)
            .Select(r => new Ventana(r.HoraInicio!.Value, r.HoraFin!.Value))
            .ToList();

        var bloqueos = reglas
            .Where(r => r.Bloqueado && !r.TodoElDia && r.HoraInicio != null && r.HoraFin != null)
            .Select(r => new Ventana(r.HoraInicio!.Value, r.HoraFin!.Value))
            .ToList();

        var turnosDelDia = await _db.Turnos
            .IgnoreQueryFilters()
            .Where(t => t.OdontologoId == odontologoId &&
                t.FechaHora.Date == fechaSolo &&
                t.Estado != TurnoEstado.Cancelado)
            .ToListAsync(ct);

        var ocupados = turnosDelDia
            .Select(t => new Ventana(t.FechaHora.TimeOfDay, t.FechaHora.TimeOfDay + TimeSpan.FromMinutes(t.DuracionMinutos)))
            .ToList();

        var slots = DisponibilidadCalculator.CalcularSlotsDisponibles(habilitadas, bloqueos, ocupados, duracionMinutos);

        return Ok(new
        {
            fecha = fechaSolo.ToString("yyyy-MM-dd"),
            duracionMinutos,
            slots = slots.Select(s => s.ToString(@"hh\:mm")).ToList()
        });
    }

    public record CrearTurnoRequest(Guid PacienteId, Guid OdontologoId, Guid? TipoTratamientoId, DateTime FechaHora);

    [HttpPost("clinicas/{slug}/turnos")]
    public async Task<IActionResult> CrearTurno(string slug, CrearTurnoRequest request, CancellationToken ct)
    {
        var tenant = await _db.Tenants.FirstOrDefaultAsync(t => t.Slug == slug && t.Estado == TenantEstado.Activo, ct);
        if (tenant is null) return NotFound(new { message = "Clínica no encontrada o no disponible." });

        var paciente = await _db.Pacientes
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(p => p.Id == request.PacienteId && p.TenantId == tenant.Id, ct);
        if (paciente is null) return BadRequest(new { message = "Paciente inválido para esta clínica." });

        var odontologo = await _db.Odontologos
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(o => o.Id == request.OdontologoId && o.TenantId == tenant.Id, ct);
        if (odontologo is null) return BadRequest(new { message = "Odontólogo inválido para esta clínica." });

        var duracionMinutos = 30;
        if (request.TipoTratamientoId is Guid ttId)
        {
            var tipoTratamiento = await _db.TiposTratamiento
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(t => t.Id == ttId && t.TenantId == tenant.Id, ct);
            if (tipoTratamiento is null) return BadRequest(new { message = "Tipo de tratamiento inválido." });
            duracionMinutos = tipoTratamiento.DuracionMinutos;
        }

        var fin = request.FechaHora.AddMinutes(duracionMinutos);
        var haySolapamiento = await _db.Turnos
            .IgnoreQueryFilters()
            .AnyAsync(t =>
                t.OdontologoId == request.OdontologoId &&
                t.Estado != TurnoEstado.Cancelado &&
                t.FechaHora < fin &&
                request.FechaHora < t.FechaHora.AddMinutes(t.DuracionMinutos), ct);

        if (haySolapamiento)
            return Conflict(new { message = "Ese horario ya no está disponible." });

        var turno = new Turno
        {
            TenantId = tenant.Id,
            OdontologoId = request.OdontologoId,
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
}
