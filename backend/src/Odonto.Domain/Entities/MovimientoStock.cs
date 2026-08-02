namespace Odonto.Domain.Entities;

/// <summary>
/// Un movimiento de stock de un insumo: Cantidad positiva es entrada
/// (compra, reposición), negativa es salida (uso, merma). El StockActual
/// del Insumo se actualiza en el mismo momento que se crea el movimiento.
/// </summary>
public class MovimientoStock
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid TenantId { get; set; }
    public Tenant Tenant { get; set; } = null!;

    public Guid InsumoId { get; set; }
    public Insumo Insumo { get; set; } = null!;

    public decimal Cantidad { get; set; }
    public string? Motivo { get; set; }

    public DateTime Fecha { get; set; } = DateTime.UtcNow;
}
