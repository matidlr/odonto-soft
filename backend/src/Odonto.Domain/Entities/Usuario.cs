using Odonto.Domain.Common;

namespace Odonto.Domain.Entities;

public class Usuario
{
    public Guid Id { get; set; } = Guid.NewGuid();

    // Null para el SuperAdmin, que no pertenece a ningún tenant.
    public Guid? TenantId { get; set; }
    public Tenant? Tenant { get; set; }

    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public Rol Rol { get; set; }
    public bool EstaActivo { get; set; } = true;
    public DateTime FechaCreacion { get; set; } = DateTime.UtcNow;
}
