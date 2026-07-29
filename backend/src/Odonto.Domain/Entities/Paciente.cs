namespace Odonto.Domain.Entities;

public class Paciente
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid TenantId { get; set; }
    public Tenant Tenant { get; set; } = null!;

    public Guid? OdontologoPrincipalId { get; set; }
    public Odontologo? OdontologoPrincipal { get; set; }

    public string Nombre { get; set; } = string.Empty;
    public string? Dni { get; set; }
    public string? Telefono { get; set; }
    public string? Email { get; set; }
    public DateTime? FechaNacimiento { get; set; }
    public DateTime FechaRegistro { get; set; } = DateTime.UtcNow;
}
