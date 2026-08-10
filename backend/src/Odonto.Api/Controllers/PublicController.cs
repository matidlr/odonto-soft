using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Odonto.Api.Validacion;
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
[Route("api/v1/public")]
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

        if (request.Nombre.Length > 200)
            return BadRequest(new { message = "El nombre es demasiado largo." });

        if (request.Dni?.Length > 30)
            return BadRequest(new { message = "El DNI es demasiado largo." });

        if (request.Telefono?.Length > 30)
            return BadRequest(new { message = "El teléfono es demasiado largo." });

        if (!string.IsNullOrWhiteSpace(request.Email) && !Validaciones.EsEmailValido(request.Email))
            return BadRequest(new { message = "El email no tiene un formato válido." });

        // No confiar en la fecha que manda el navegador: no puede ser futura
        // (nadie nace en el futuro) ni absurdamente antigua (típico error de
        // tipeo, ej. año 1901 en vez de 1991).
        if (request.FechaNacimiento is DateTime fechaNacimiento &&
            (fechaNacimiento.Date > DateTime.UtcNow.Date || fechaNacimiento.Year < 1900))
        {
            return BadRequest(new { message = "La fecha de nacimiento no es válida." });
        }

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
            .Select(o => new { o.Id, o.Nombre, o.Especialidad })
            .ToListAsync(ct);

        return Ok(odontologos);
    }

    [HttpGet("clinicas/{slug}/odontologos/{odontologoId}/sedes")]
    public async Task<IActionResult> GetSedes(string slug, Guid odontologoId, CancellationToken ct)
    {
        var tenant = await _db.Tenants.FirstOrDefaultAsync(t => t.Slug == slug && t.Estado == TenantEstado.Activo, ct);
        if (tenant is null) return NotFound(new { message = "Clínica no encontrada o no disponible." });

        var sedes = await _db.Sedes
            .IgnoreQueryFilters()
            .Where(s => s.OdontologoId == odontologoId && s.TenantId == tenant.Id && s.Activa)
            .OrderByDescending(s => s.EsPrincipal).ThenBy(s => s.Nombre)
            .Select(s => new { s.Id, s.Nombre, s.Direccion, s.EsPrincipal })
            .ToListAsync(ct);

        return Ok(sedes);
    }

    /// <summary>Sede a usar cuando no se especifica una: la Principal del odontólogo.</summary>
    private async Task<Guid?> ResolverSedeId(Guid odontologoId, Guid? sedeIdPedido, CancellationToken ct)
    {
        if (sedeIdPedido is Guid pedida) return pedida;

        var principal = await _db.Sedes
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(s => s.OdontologoId == odontologoId && s.EsPrincipal, ct);
        return principal?.Id;
    }

    [HttpGet("clinicas/{slug}/odontologos/{odontologoId}/horarios-disponibles")]
    public async Task<IActionResult> GetHorariosDisponibles(
        string slug,
        Guid odontologoId,
        [FromQuery] DateTime fecha,
        [FromQuery] Guid? tipoTratamientoId,
        [FromQuery] Guid? sedeId,
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

        var sedeActualId = await ResolverSedeId(odontologoId, sedeId, ct);

        var fechaSolo = fecha.Date;
        var diaSemana = fechaSolo.DayOfWeek.ADiaSemana();

        var reglas = await _db.Disponibilidades
            .IgnoreQueryFilters()
            .Where(d => d.OdontologoId == odontologoId && d.SedeId == sedeActualId &&
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

    public record CrearTurnoRequest(Guid PacienteId, Guid OdontologoId, Guid? SedeId, Guid? TipoTratamientoId, DateTime FechaHora);

    [HttpPost("clinicas/{slug}/turnos")]
    public async Task<IActionResult> CrearTurno(string slug, CrearTurnoRequest request, CancellationToken ct)
    {
        // No confiar en la fecha que manda el navegador (esto es un endpoint
        // anónimo): nada de turnos en el pasado ni años de anticipación, que
        // es lo que pasaría si alguien manda cualquier cosa a mano.
        if (request.FechaHora < DateTime.UtcNow.AddMinutes(-5) || request.FechaHora > DateTime.UtcNow.AddYears(1))
            return BadRequest(new { message = "La fecha del turno no es válida." });

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

        if (request.SedeId is Guid sedeIdPedida)
        {
            var sedeValida = await _db.Sedes
                .IgnoreQueryFilters()
                .AnyAsync(s => s.Id == sedeIdPedida && s.TenantId == tenant.Id && s.OdontologoId == request.OdontologoId, ct);
            if (!sedeValida) return BadRequest(new { message = "Sede inválida para este odontólogo." });
        }

        // Igual que en la reserva manual: nunca se filtra por sede acá, para
        // que el odontólogo jamás quede agendado a la misma hora en dos
        // sedes distintas.
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

        var sedeId = await ResolverSedeId(request.OdontologoId, request.SedeId, ct);

        var turno = new Turno
        {
            TenantId = tenant.Id,
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
}
