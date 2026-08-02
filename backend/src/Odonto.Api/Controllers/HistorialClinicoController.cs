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
[Route("api/pacientes/{pacienteId}")]
[Authorize(Policy = "TenantActivo")]
public class HistorialClinicoController : ControllerBase
{
    private readonly AppDbContext _db;

    public HistorialClinicoController(AppDbContext db)
    {
        _db = db;
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
        if (ficha is null)
        {
            ficha = new FichaMedica { TenantId = tenantId, PacienteId = pacienteId };
            _db.FichasMedicas.Add(ficha);
        }

        ficha.Alergias = request.Alergias;
        ficha.EnfermedadesPreexistentes = request.EnfermedadesPreexistentes;
        ficha.MedicacionActual = request.MedicacionActual;
        ficha.Habitos = request.Habitos;
        ficha.Observaciones = request.Observaciones;
        ficha.FechaActualizacion = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);

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
        await _db.SaveChangesAsync(ct);

        return Ok(new { nota.Id, nota.Fecha, nota.TurnoId });
    }
}
