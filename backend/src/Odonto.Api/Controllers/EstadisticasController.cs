using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Odonto.Domain.Common;
using Odonto.Infrastructure.Persistence;

namespace Odonto.Api.Controllers;

/// <summary>
/// Dashboard de estadísticas de la clínica. "Desde/Hasta" filtran pacientes
/// nuevos, turnos y cancelaciones; la facturación mensual y los rankings de
/// tratamientos/odontólogos se calculan sobre los últimos 6 meses siempre,
/// para poder ver una tendencia sin depender del filtro puntual.
/// </summary>
[ApiController]
[Route("api/v1/estadisticas")]
[Authorize(Policy = "TenantActivo")]
public class EstadisticasController : ControllerBase
{
    private readonly AppDbContext _db;

    public EstadisticasController(AppDbContext db)
    {
        _db = db;
    }

    public record FacturacionMesResponse(int Anio, int Mes, decimal Total);
    public record TratamientoRankingResponse(string Nombre, int Cantidad);
    public record OdontologoRankingResponse(Guid OdontologoId, string Nombre, int Cantidad);
    public record TurnosPorEstadoResponse(TurnoEstado Estado, int Cantidad);

    public record EstadisticasResponse(
        DateTime Desde,
        DateTime Hasta,
        int PacientesNuevos,
        int CantidadTurnos,
        int Cancelaciones,
        List<TurnosPorEstadoResponse> TurnosPorEstado,
        decimal FacturacionTotalPeriodo,
        List<FacturacionMesResponse> FacturacionPorMes,
        List<TratamientoRankingResponse> TratamientosMasRealizados,
        List<OdontologoRankingResponse> OdontologosConMasConsultas);

    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] DateTime? desde, [FromQuery] DateTime? hasta, CancellationToken ct)
    {
        var hastaValor = (hasta ?? DateTime.UtcNow).Date.AddDays(1).AddTicks(-1);
        var desdeValor = (desde ?? new DateTime(hastaValor.Year, hastaValor.Month, 1)).Date;

        var pacientesNuevos = await _db.Pacientes
            .CountAsync(p => p.FechaRegistro >= desdeValor && p.FechaRegistro <= hastaValor, ct);

        var turnosEnRango = await _db.Turnos
            .Where(t => t.FechaHora >= desdeValor && t.FechaHora <= hastaValor)
            .Select(t => t.Estado)
            .ToListAsync(ct);

        var turnosPorEstado = turnosEnRango
            .GroupBy(e => e)
            .Select(g => new TurnosPorEstadoResponse(g.Key, g.Count()))
            .OrderByDescending(r => r.Cantidad)
            .ToList();

        var cancelaciones = turnosEnRango.Count(e => e == TurnoEstado.Cancelado);

        // Facturación y rankings: siempre sobre los últimos 6 meses, para
        // que el dashboard muestre una tendencia aunque el filtro de fecha
        // puntual sea más corto.
        var inicioTendencia = new DateTime(hastaValor.Year, hastaValor.Month, 1).AddMonths(-5);

        var facturacionTotalPeriodo = await _db.Cobros
            .Where(c => c.Fecha >= desdeValor && c.Fecha <= hastaValor)
            .SumAsync(c => (decimal?)c.Monto, ct) ?? 0m;

        var cobrosTendencia = await _db.Cobros
            .Where(c => c.Fecha >= inicioTendencia)
            .Select(c => new { c.Fecha, c.Monto })
            .ToListAsync(ct);

        var facturacionPorMes = cobrosTendencia
            .GroupBy(c => new { c.Fecha.Year, c.Fecha.Month })
            .Select(g => new FacturacionMesResponse(g.Key.Year, g.Key.Month, g.Sum(x => x.Monto)))
            .OrderBy(r => r.Anio).ThenBy(r => r.Mes)
            .ToList();

        var turnosCompletadosTendencia = await _db.Turnos
            .Where(t => t.FechaHora >= inicioTendencia && t.Estado == TurnoEstado.Completado)
            .Select(t => new { t.OdontologoId, OdontologoNombre = t.Odontologo.Nombre, TratamientoNombre = t.TipoTratamiento != null ? t.TipoTratamiento.Nombre : null })
            .ToListAsync(ct);

        var tratamientosMasRealizados = turnosCompletadosTendencia
            .Where(t => t.TratamientoNombre != null)
            .GroupBy(t => t.TratamientoNombre!)
            .Select(g => new TratamientoRankingResponse(g.Key, g.Count()))
            .OrderByDescending(r => r.Cantidad)
            .Take(5)
            .ToList();

        var odontologosConMasConsultas = turnosCompletadosTendencia
            .GroupBy(t => new { t.OdontologoId, t.OdontologoNombre })
            .Select(g => new OdontologoRankingResponse(g.Key.OdontologoId, g.Key.OdontologoNombre, g.Count()))
            .OrderByDescending(r => r.Cantidad)
            .Take(5)
            .ToList();

        return Ok(new EstadisticasResponse(
            desdeValor,
            hastaValor,
            pacientesNuevos,
            turnosEnRango.Count,
            cancelaciones,
            turnosPorEstado,
            facturacionTotalPeriodo,
            facturacionPorMes,
            tratamientosMasRealizados,
            odontologosConMasConsultas));
    }
}
