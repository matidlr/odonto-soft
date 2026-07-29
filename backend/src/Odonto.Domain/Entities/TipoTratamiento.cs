namespace Odonto.Domain.Entities;

public class TipoTratamiento
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid TenantId { get; set; }
    public Tenant Tenant { get; set; } = null!;

    public string Nombre { get; set; } = string.Empty;
    public int DuracionMinutos { get; set; }
    public decimal PrecioBase { get; set; }
}
