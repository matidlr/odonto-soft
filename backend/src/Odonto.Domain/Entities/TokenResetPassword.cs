namespace Odonto.Domain.Entities;

/// <summary>
/// Token de un solo uso para el flujo de "olvidé mi contraseña". No lleva
/// TenantId ni query filter: es una tabla de sistema, no un dato de negocio
/// de ningún tenant en particular.
/// </summary>
public class TokenResetPassword
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid UsuarioId { get; set; }
    public Usuario Usuario { get; set; } = null!;

    public string Token { get; set; } = string.Empty;
    public DateTime FechaExpiracion { get; set; }
    public bool Usado { get; set; }
    public DateTime FechaCreacion { get; set; } = DateTime.UtcNow;
}
