using Odonto.Domain.Common;

namespace Odonto.Domain.Entities;

public class Tenant
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Nombre { get; set; } = string.Empty;

    // Usado en el link público de registro de pacientes: /r/{Slug}
    public string Slug { get; set; } = string.Empty;

    public TenantEstado Estado { get; set; } = TenantEstado.PendienteDeActivacion;

    // Referencia a la suscripción de Mercado Pago asociada a este tenant.
    public string? MercadoPagoPreapprovalId { get; set; }
    public Guid? PlanId { get; set; }

    public DateTime FechaAlta { get; set; } = DateTime.UtcNow;

    public ICollection<Usuario> Usuarios { get; set; } = new List<Usuario>();
    public ICollection<Paciente> Pacientes { get; set; } = new List<Paciente>();
}
