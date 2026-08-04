namespace Odonto.Domain.Entities;

/// <summary>
/// Refresh token de sesión (uno por dispositivo/navegador donde el usuario
/// inició sesión). Nunca guardamos el token en texto plano, solo su hash:
/// si alguien accede a la base no puede usarlo directamente. No lleva
/// TenantId ni query filter: es una tabla de sistema, consultada en
/// endpoints anónimos (login/refresh), igual que TokenResetPassword.
/// </summary>
public class RefreshToken
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid UsuarioId { get; set; }
    public Usuario Usuario { get; set; } = null!;

    public string TokenHash { get; set; } = string.Empty;
    public DateTime FechaCreacion { get; set; } = DateTime.UtcNow;
    public DateTime FechaExpiracion { get; set; }
    public bool Revocado { get; set; }
    public DateTime? FechaRevocado { get; set; }

    // Para el aviso de "inicio de sesión desde dispositivo nuevo" y para
    // mostrar la lista de sesiones activas (con posibilidad de cerrar una
    // en particular). Nunca se usan para autorizar nada, solo informativos.
    public string? UserAgent { get; set; }
    public string? IpAddress { get; set; }
}
