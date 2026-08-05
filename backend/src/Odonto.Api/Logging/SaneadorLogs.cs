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
}
