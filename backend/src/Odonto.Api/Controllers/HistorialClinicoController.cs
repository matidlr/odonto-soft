using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Odonto.Domain.Entities;
using Odonto.Infrastructure.Persistence;

namespace Odonto.Api.Controllers;

/// <summary>
/// Historial clínico general del paciente: la ficha médica (una fila fija
/// por paciente, se edita) y las notas de evolución (lista cronológica de
/// consultas, no atadas a un diente puntual — para eso está el odontograma).
/// </summary>
[ApiController]
[Route("api/v1/pacientes/{pacienteId}")]
[Authorize(Policy = "TenantActivo")]
public class HistorialClinicoController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ILogger<HistorialClinicoController> _logger;

    public HistorialClinicoController(AppDbContext db, ILogger<HistorialClinicoController> logger)
    {
        _db = db;
        _logger = logger;
    }

    // Para el log de la app (ILogger) solo nos interesa quién hizo el
    // cambio, no qué escribió: el contenido médico nunca va a un log de
    // texto. El detalle campo por campo (valor anterior/nuevo) sí se
    // guarda, pero en RegistrosAuditoria — la misma base protegida y
    // aislada por tenant que el resto de la historia clínica, no un log.
    private Guid? UsuarioIdActual()
    {
        var claim = User.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)?.Value;
        return Guid.TryParse(claim, out var id) ? id : null;
    }

    /// <summary>Agrega una fila de auditoría si el valor realmente cambió (evita ruido).</summary>
    private void AuditarCampo(Guid tenantId, Guid pacienteId, string entidad, Guid entidadId, string accion, string campo, string? anterior, string? nuevo)
    {
        if (anterior == nuevo) return;

        _db.RegistrosAuditoria.Add(new RegistroAuditoria
        {
            TenantId = tenantId,
            PacienteId = pacienteId,
            UsuarioId = UsuarioIdActual(),
            Entidad = entidad,
            EntidadId = entidadId,
            Accion = accion,
            Campo = campo,
            ValorAnterior = anterior,
            ValorNuevo = nuevo
        });
    }

    public record FichaMedicaResponse(
        string? Alergias,
        string? EnfermedadesPreexistentes,
        string? MedicacionActual,
        string? Habitos,
        string? Observaciones,
        DateTime? FechaActualizacion);

    [HttpGet("ficha-medica")]
    public async Task<IActionResult> GetFichaMedica(Guid pacienteId, CancellationToken ct)
    {
        var paciente = await _db.Pacientes.FirstOrDefaultAsync(p => p.Id == pacienteId, ct);
        if (paciente is null) return NotFound(new { message = "Paciente no encontrado." });

        var ficha = await _db.FichasMedicas.FirstOrDefaultAsync(f => f.PacienteId == pacienteId, ct);

        return Ok(ficha is null
            ? new FichaMedicaResponse(null, null, null, null, null, null)
            : new FichaMedicaResponse(
                ficha.Alergias,
                ficha.EnfermedadesPreexistentes,
                ficha.MedicacionActual,
                ficha.Habitos,
                ficha.Observaciones,
                ficha.FechaActualizacion));
    }

    public record GuardarFichaMedicaRequest(
        string? Alergias,
        string? EnfermedadesPreexistentes,
        string? MedicacionActual,
        string? Habitos,
        string? Observaciones);

    [HttpPut("ficha-medica")]
    public async Task<IActionResult> GuardarFichaMedica(Guid pacienteId, GuardarFichaMedicaRequest request, CancellationToken ct)
    {
        var paciente = await _db.Pacientes.FirstOrDefaultAsync(p => p.Id == pacienteId, ct);
        if (paciente is null) return NotFound(new { message = "Paciente no encontrado." });

        var tenantIdClaim = User.FindFirst("tenant_id")?.Value;
        if (!Guid.TryParse(tenantIdClaim, out var tenantId))
            return BadRequest(new { message = "No se pudo determinar el tenant del usuario." });

        var ficha = await _db.FichasMedicas.FirstOrDefaultAsync(f => f.PacienteId == pacienteId, ct);
        var esNueva = ficha is null;
        if (ficha is null)
        {
            ficha = new FichaMedica { TenantId = tenantId, PacienteId = pacienteId };
            _db.FichasMedicas.Add(ficha);
        }

        var accion = esNueva ? "Creado" : "Editado";

        // Un renglón de auditoría por campo que realmente cambió, con el
        // valor de antes y el de ahora — acá sí va el contenido, a
        // diferencia del log de la app.
        AuditarCampo(tenantId, pacienteId, "FichaMedica", ficha.Id, accion, "Alergias", ficha.Alergias, request.Alergias);
        AuditarCampo(tenantId, pacienteId, "FichaMedica", ficha.Id, accion, "EnfermedadesPreexistentes", ficha.EnfermedadesPreexistentes, request.EnfermedadesPreexistentes);
        AuditarCampo(tenantId, pacienteId, "FichaMedica", ficha.Id, accion, "MedicacionActual", ficha.MedicacionActual, request.MedicacionActual);
        AuditarCampo(tenantId, pacienteId, "FichaMedica", ficha.Id, accion, "Habitos", ficha.Habitos, request.Habitos);
        AuditarCampo(tenantId, pacienteId, "FichaMedica", ficha.Id, accion, "Observaciones", ficha.Observaciones, request.Observaciones);

        ficha.Alergias = request.Alergias;
        ficha.EnfermedadesPreexistentes = request.EnfermedadesPreexistentes;
        ficha.MedicacionActual = request.MedicacionActual;
        ficha.Habitos = request.Habitos;
        ficha.Observaciones = request.Observaciones;
        ficha.FechaActualizacion = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);

        // Solo quién/cuándo/a qué paciente — nunca el contenido (alergias,
        // medicación, etc. son datos médicos, no van a un log).
        _logger.LogInformation("Ficha médica editada para paciente {PacienteId} por usuario {UsuarioId}",
            pacienteId, UsuarioIdActual());

        return Ok(new { message = "Ficha médica guardada." });
    }

    public record NotaEvolucionResponse(
        Guid Id,
        string? Motivo,
        string? Diagnostico,
        string? TratamientoRealizado,
        string? Evolucion,
        string? Medicacion,
        string? Observaciones,
        Guid? OdontologoId,
        Guid? TurnoId,
        DateTime Fecha);

    [HttpGet("notas-evolucion")]
    public async Task<IActionResult> GetNotasEvolucion(Guid pacienteId, CancellationToken ct)
    {
        var notas = await _db.NotasEvolucion
            .Where(n => n.PacienteId == pacienteId)
            .OrderByDescending(n => n.Fecha)
            .Select(n => new NotaEvolucionResponse(
                n.Id, n.Motivo, n.Diagnostico, n.TratamientoRealizado, n.Evolucion, n.Medicacion, n.Observaciones,
                n.OdontologoId, n.TurnoId, n.Fecha))
            .ToListAsync(ct);

        return Ok(notas);
    }

    public record CrearNotaEvolucionRequest(
        string? Motivo,
        string? Diagnostico,
        string? TratamientoRealizado,
        string? Evolucion,
        string? Medicacion,
        string? Observaciones,
        Guid? OdontologoId,
        // Igual que en el odontograma: si se pasa TurnoId, la fecha de la
        // nota es la del turno (se ignora Fecha). Si no, se usa Fecha o "ahora".
        Guid? TurnoId,
        DateTime? Fecha);

    [HttpPost("notas-evolucion")]
    public async Task<IActionResult> CrearNotaEvolucion(Guid pacienteId, CrearNotaEvolucionRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Motivo)
            && string.IsNullOrWhiteSpace(request.Diagnostico)
            && string.IsNullOrWhiteSpace(request.TratamientoRealizado)
            && string.IsNullOrWhiteSpace(request.Evolucion)
            && string.IsNullOrWhiteSpace(request.Medicacion)
            && string.IsNullOrWhiteSpace(request.Observaciones))
        {
            return BadRequest(new { message = "Completá al menos uno de los campos de la nota." });
        }

        var paciente = await _db.Pacientes.FirstOrDefaultAsync(p => p.Id == pacienteId, ct);
        if (paciente is null) return NotFound(new { message = "Paciente no encontrado." });

        if (request.OdontologoId is Guid odontologoId)
        {
            var existeOdontologo = await _db.Odontologos.AnyAsync(o => o.Id == odontologoId, ct);
            if (!existeOdontologo) return BadRequest(new { message = "Odontólogo inválido." });
        }

        var tenantIdClaim = User.FindFirst("tenant_id")?.Value;
        if (!Guid.TryParse(tenantIdClaim, out var tenantId))
            return BadRequest(new { message = "No se pudo determinar el tenant del usuario." });

        DateTime fecha;
        Guid? turnoId = null;

        if (request.TurnoId is Guid tid)
        {
            var turno = await _db.Turnos.FirstOrDefaultAsync(t => t.Id == tid && t.PacienteId == pacienteId, ct);
            if (turno is null)
                return BadRequest(new { message = "El turno indicado no existe o no pertenece a este paciente." });

            turnoId = turno.Id;
            fecha = turno.FechaHora;
        }
        else
        {
            fecha = request.Fecha ?? DateTime.UtcNow;
        }

        var nota = new NotaEvolucion
        {
            TenantId = tenantId,
            PacienteId = pacienteId,
            Motivo = request.Motivo,
            Diagnostico = request.Diagnostico,
            TratamientoRealizado = request.TratamientoRealizado,
            Evolucion = request.Evolucion,
            Medicacion = request.Medicacion,
            Observaciones = request.Observaciones,
            OdontologoId = request.OdontologoId,
            TurnoId = turnoId,
            Fecha = fecha
        };

        _db.NotasEvolucion.Add(nota);

        // Una fila de auditoría por campo cargado (ValorAnterior vacío,
        // ValorNuevo lo que escribió). Se guardan junto con la nota, en la
        // misma transacción.
        AuditarCampo(tenantId, pacienteId, "NotaEvolucion", nota.Id, "Creado", "Motivo", null, request.Motivo);
        AuditarCampo(tenantId, pacienteId, "NotaEvolucion", nota.Id, "Creado", "Diagnostico", null, request.Diagnostico);
        AuditarCampo(tenantId, pacienteId, "NotaEvolucion", nota.Id, "Creado", "TratamientoRealizado", null, request.TratamientoRealizado);
        AuditarCampo(tenantId, pacienteId, "NotaEvolucion", nota.Id, "Creado", "Evolucion", null, request.Evolucion);
        AuditarCampo(tenantId, pacienteId, "NotaEvolucion", nota.Id, "Creado", "Medicacion", null, request.Medicacion);
        AuditarCampo(tenantId, pacienteId, "NotaEvolucion", nota.Id, "Creado", "Observaciones", null, request.Observaciones);

        await _db.SaveChangesAsync(ct);

        // Igual que arriba: solo metadata, nunca el contenido de la nota.
        _logger.LogInformation("Nota de evolución agregada (id {NotaId}) para paciente {PacienteId} por usuario {UsuarioId}",
            nota.Id, pacienteId, UsuarioIdActual());

        return Ok(new { nota.Id, nota.Fecha, nota.TurnoId });
    }

    public record RegistroAuditoriaResponse(
        Guid Id,
        DateTime Fecha,
        string? UsuarioNombre,
        string? UsuarioEmail,
        string Entidad,
        string Accion,
        string? Campo,
        string? ValorAnterior,
        string? ValorNuevo);

    /// <summary>
    /// Auditoría de la historia clínica de este paciente: odontograma,
    /// ficha médica y notas de evolución. Quién, cuándo, qué acción y el
    /// valor anterior/nuevo cuando corresponde.
    /// </summary>
    [HttpGet("auditoria")]
    public async Task<IActionResult> GetAuditoria(Guid pacienteId, CancellationToken ct)
    {
        var paciente = await _db.Pacientes.FirstOrDefaultAsync(p => p.Id == pacienteId, ct);
        if (paciente is null) return NotFound(new { message = "Paciente no encontrado." });

        var registros = await _db.RegistrosAuditoria
            .Where(a => a.PacienteId == pacienteId)
            .OrderByDescending(a => a.Fecha)
            .Select(a => new RegistroAuditoriaResponse(
                a.Id,
                a.Fecha,
                a.Usuario != null ? a.Usuario.Nombre : null,
                a.Usuario != null ? a.Usuario.Email : null,
                a.Entidad,
                a.Accion,
                a.Campo,
                a.ValorAnterior,
                a.ValorNuevo))
            .ToListAsync(ct);

        return Ok(registros);
    }
}
