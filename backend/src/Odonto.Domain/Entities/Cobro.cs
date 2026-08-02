using Odonto.Domain.Common;

namespace Odonto.Domain.Entities;

/// <summary>
/// Un pago registrado de un paciente (efectivo, transferencia, tarjeta o
/// QR). Puede asociarse opcionalmente a un presupuesto puntual, para saber
/// contra qué se está pagando; si no, es un pago suelto. El saldo del
/// paciente se calcula en el controller: suma de presupuestos Aprobados
/// menos suma de cobros.
/// </summary>
public class Cobro
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid TenantId { get; set; }
    public Tenant Tenant { get; set; } = null!;

    public Guid PacienteId { get; set; }
    public Paciente Paciente { get; set; } = null!;

    public Guid? PresupuestoId { get; set; }
    public Presupuesto? Presupuesto { get; set; }

    public Guid? OdontologoId { get; set; }
    public Odontologo? Odontologo { get; set; }

    public decimal Monto { get; set; }
    public MedioPago MedioPago { get; set; }
    public string? Concepto { get; set; }

    public DateTime Fecha { get; set; } = DateTime.UtcNow;
}
