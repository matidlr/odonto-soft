namespace Odonto.Api.Logging;

/// <summary>
/// Saca \r y \n de un valor antes de usarlo en un mensaje de log. Sin esto,
/// alguien podría mandar un valor con saltos de línea embebidos (un query
/// param, un header, un campo del body) para que el archivo de log parezca
/// tener líneas que en realidad no existen (log injection / log forging).
/// Usar en cualquier valor que venga del request y termine en un log.
/// </summary>
public static class SaneadorLogs
{
    public static string? Limpiar(string? valor) => valor?.Replace("\r", "").Replace("\n", "");

    /// <summary>
    /// Enmascara un email para poder loguearlo sin exponer el dato completo
    /// (ej. "ma***@gmail.com"): alcanza para reconocer patrones (¿es la
    /// misma cuenta que ya falló antes?) sin guardar el email real en un
    /// archivo que puede vivir en disco 30 días. También lo sanea de \r\n,
    /// ya que en el login todavía no se validó el formato del email.
    /// </summary>
    public static string EnmascararEmail(string? email)
    {
        email = Limpiar(email);
        if (string.IsNullOrWhiteSpace(email)) return "(vacío)";

        var arroba = email.IndexOf('@');
        if (arroba <= 0) return "***"; // no tiene forma de email, no arriesgamos mostrarlo

        var usuario = email[..arroba];
        var dominio = email[arroba..];
        var visible = usuario.Length <= 2 ? usuario[..1] : usuario[..2];
        return $"{visible}***{dominio}";
    }
}
