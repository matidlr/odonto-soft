using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Odonto.Domain.Common;
using Odonto.Domain.Entities;
using Odonto.Infrastructure.Persistence;

namespace Odonto.Api.Controllers;

/// <summary>
/// Cobros a pacientes (efectivo, transferencia, tarjeta, QR) y cálculo de
/// saldo: lo que un paciente debe sale de sumar sus presupuestos Aprobados
/// y restar lo que ya pagó. No hace falta que cada cobro esté atado a un
/// presupuesto puntual, pero si lo está, queda trazado contra qué se pagó.
/// </summary>
[ApiController]
[Authorize(Policy = "TenantActivo")]
public class CobrosController : ControllerBase
{
    private readonly AppDbContext _db;

    public CobrosController(AppDbContext db)
    {
        _db = db;
    }

    public record CobroResponse(
        Guid Id,
        Guid PacienteId,
        Guid? PresupuestoId,
        Guid? OdontologoId,
        decimal Monto,
        MedioPago MedioPago,
        string? Concepto,
        DateTime Fecha);

    private static CobroResponse AResponse(Cobro c) => new(
        c.Id, c.PacienteId, c.PresupuestoId, c.OdontologoId, c.Monto, c.MedioPago, c.Concepto, c.Fecha);

    public record SaldoResponse(decimal TotalAprobado, decimal TotalCobrado, decimal Saldo);

    private async Task<SaldoResponse> CalcularSaldo(Guid pacienteId, CancellationToken ct)
    {
        var totalAprobado = await _db.Presupuestos
            .Where(p => p.PacienteId == pacienteId && p.Estado == EstadoPresupuesto.Aprobado)
            .SelectMany(p => p.Items)
            .SumAsync(i => (decimal?)(i.Cantidad * i.PrecioUnitario), ct) ?? 0m;

        var totalCobrado = await _db.Cobros
            .Where(c => c.PacienteId == pacienteId)
            .SumAsync(c => (decimal?)c.Monto, ct) ?? 0m;

        return new SaldoResponse(totalAprobado, totalCobrado, totalAprobado - totalCobrado);
    }

    [HttpGet("api/pacientes/{pacienteId}/saldo")]
    public async Task<IActionResult> GetSaldo(Guid pacienteId, CancellationToken ct)
    {
        var paciente = await _db.Pacientes.FirstOrDefaultAsync(p => p.Id == pacienteId, ct);
        if (paciente is null) return NotFound(new { message = "Paciente no encontrado." });

        return Ok(await CalcularSaldo(pacienteId, ct));
    }

    [HttpGet("api/pacientes/{pacienteId}/cobros")]
    public async Task<IActionResult> GetPorPaciente(Guid pacienteId, CancellationToken ct)
    {
        var paciente = await _db.Pacientes.FirstOrDefaultAsync(p => p.Id == pacienteId, ct);
        if (paciente is null) return NotFound(new { message = "Paciente no encontrado." });

        var cobros = await _db.Cobros
            .Where(c => c.PacienteId == pacienteId)
            .OrderByDescending(c => c.Fecha)
            .ToListAsync(ct);

        return Ok(cobros.Select(AResponse));
    }

    public record CrearCobroRequest(
        decimal Monto,
        MedioPago MedioPago,
        string? Concepto,
        Guid? PresupuestoId,
        Guid? OdontologoId,
        DateTime? Fecha);

    [HttpPost("api/pacientes/{pacienteId}/cobros")]
    public async Task<IActionResult> Crear(Guid pacienteId, CrearCobroRequest request, CancellationToken ct)
    {
        if (request.Monto <= 0)
            return BadRequest(new { message = "El monto tiene que ser mayor a 0." });

        var paciente = await _db.Pacientes.FirstOrDefaultAsync(p => p.Id == pacienteId, ct);
        if (paciente is null) return NotFound(new { message = "Paciente no encontrado." });

        if (request.PresupuestoId is Guid presupuestoId)
        {
            var presupuesto = await _db.Presupuestos.FirstOrDefaultAsync(p => p.Id == presupuestoId && p.PacienteId == pacienteId, ct);
            if (presupuesto is null)
                return BadRequest(new { message = "El presupuesto indicado no existe o no pertenece a este paciente." });
        }

        if (request.OdontologoId is Guid odontologoId)
        {
            var existeOdontologo = await _db.Odontologos.AnyAsync(o => o.Id == odontologoId, ct);
            if (!existeOdontologo) return BadRequest(new { message = "Odontólogo inválido." });
        }

        var tenantIdClaim = User.FindFirst("tenant_id")?.Value;
        if (!Guid.TryParse(tenantIdClaim, out var tenantId))
            return BadRequest(new { message = "No se pudo determinar el tenant del usuario." });

        var cobro = new Cobro
        {
            TenantId = tenantId,
            PacienteId = pacienteId,
            PresupuestoId = request.PresupuestoId,
            OdontologoId = request.OdontologoId,
            Monto = request.Monto,
            MedioPago = request.MedioPago,
            Concepto = request.Concepto,
            Fecha = request.Fecha ?? DateTime.UtcNow
        };

        _db.Cobros.Add(cobro);
        await _db.SaveChangesAsync(ct);

        return Ok(AResponse(cobro));
    }

    [HttpDelete("api/cobros/{id}")]
    public async Task<IActionResult> Borrar(Guid id, CancellationToken ct)
    {
        var cobro = await _db.Cobros.FirstOrDefaultAsync(c => c.Id == id, ct);
        if (cobro is null) return NotFound();

        _db.Cobros.Remove(cobro);
        await _db.SaveChangesAsync(ct);

        return Ok(new { message = "Cobro eliminado." });
    }

    public record PacientePendienteResponse(Guid PacienteId, string PacienteNombre, decimal TotalAprobado, decimal TotalCobrado, decimal Saldo);

    /// <summary>Pacientes con saldo pendiente (presupuestos aprobados que todavía no se cobraron del todo).</summary>
    [HttpGet("api/cobros/pendientes")]
    public async Task<IActionResult> GetPendientes(CancellationToken ct)
    {
        var aprobadosPorPaciente = await _db.Presupuestos
            .Where(p => p.Estado == EstadoPresupuesto.Aprobado)
            .SelectMany(p => p.Items, (p, i) => new { p.PacienteId, Monto = i.Cantidad * i.PrecioUnitario })
            .GroupBy(x => x.PacienteId)
            .Select(g => new { PacienteId = g.Key, Total = g.Sum(x => x.Monto) })
            .ToListAsync(ct);

        if (aprobadosPorPaciente.Count == 0) return Ok(Array.Empty<PacientePendienteResponse>());

        var pacienteIds = aprobadosPorPaciente.Select(a => a.PacienteId).ToList();

        var cobradoPorPaciente = await _db.Cobros
            .Where(c => pacienteIds.Contains(c.PacienteId))
            .GroupBy(c => c.PacienteId)
            .Select(g => new { PacienteId = g.Key, Total = g.Sum(c => c.Monto) })
            .ToListAsync(ct);

        var nombresPorPaciente = await _db.Pacientes
            .Where(p => pacienteIds.Contains(p.Id))
            .Select(p => new { p.Id, p.Nombre })
            .ToDictionaryAsync(p => p.Id, p => p.Nombre, ct);

        var cobradoDict = cobradoPorPaciente.ToDictionary(c => c.PacienteId, c => c.Total);

        var resultado = aprobadosPorPaciente
            .Select(a =>
            {
                var cobrado = cobradoDict.TryGetValue(a.PacienteId, out var total) ? total : 0m;
                return new PacientePendienteResponse(
                    a.PacienteId,
                    nombresPorPaciente.TryGetValue(a.PacienteId, out var nombre) ? nombre : "(paciente)",
                    a.Total,
                    cobrado,
                    a.Total - cobrado);
            })
            .Where(r => r.Saldo > 0)
            .OrderByDescending(r => r.Saldo)
            .ToList();

        return Ok(resultado);
    }
}
