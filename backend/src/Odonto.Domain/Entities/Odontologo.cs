namespace Odonto.Domain.Entities;

public class Odontologo
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid TenantId { get; set; }
    public Tenant Tenant { get; set; } = null!;

    public Guid UsuarioId { get; set; }
    public Usuario Usuario { get; set; } = null!;

    public string Matricula { get; set; } = string.Empty;
    public string? Especialidad { get; set; }

    // Color usado para diferenciarlo en la agenda compartida del tenant.
    public string ColorAgenda { get; set; } = "#2563eb";
}
