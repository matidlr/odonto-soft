using Odonto.Domain.Common;

namespace Odonto.Domain.Entities;

/// <summary>
/// Un insumo de la clínica (anestesia, guantes, resinas, implantes,
/// materiales, etc.). StockActual solo se modifica a través de
/// MovimientoStock, nunca se edita directo, para que quede historial de
/// cada entrada/salida.
/// </summary>
public class Insumo
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid TenantId { get; set; }
    public Tenant Tenant { get; set; } = null!;

    public string Nombre { get; set; } = string.Empty;
    public CategoriaInsumo Categoria { get; set; }
    public string Unidad { get; set; } = "unidades";

    public decimal StockActual { get; set; }
    public decimal StockMinimo { get; set; }

    public bool Activo { get; set; } = true;
}
