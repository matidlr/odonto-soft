namespace Odonto.Application.Common.Interfaces;

public record SaldoPaciente(decimal TotalAprobado, decimal TotalCobrado, decimal Saldo);

/// <summary>
/// Cálculo de saldo de un paciente: lo que debe = suma de sus presupuestos
/// Aprobados menos lo que ya pagó. Vive en un servicio aparte (no en el
/// controller) para que, si mañana hace falta este mismo cálculo desde otro
/// lugar (un reporte, un recordatorio de deuda pendiente), no haya que
/// duplicar la lógica.
/// </summary>
public interface ICobroService
{
    Task<SaldoPaciente> CalcularSaldoAsync(Guid pacienteId, CancellationToken ct = default);
}
