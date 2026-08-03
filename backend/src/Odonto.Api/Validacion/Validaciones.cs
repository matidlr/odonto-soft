using System.Text.RegularExpressions;

namespace Odonto.Api.Validacion;

/// <summary>
/// Helpers de validación server-side compartidos por los controllers. Existen
/// porque el frontend (Angular) valida para dar buena experiencia de uso,
/// pero nunca hay que confiar en eso: cualquiera puede llamar a la API
/// directamente (con curl, Postman, etc.) saltándose el formulario. Todo lo
/// que importa se tiene que volver a validar acá.
/// </summary>
public static class Validaciones
{
    // Regex simple y permisiva a propósito: alcanza para descartar strings
    // que claramente no son un email (sin @, sin dominio), sin rechazar
    // direcciones raras pero válidas. La validación fuerte de verdad es que
    // el email funcione al mandar un correo.
    private static readonly Regex FormatoEmail = new(
        @"^[^@\s]+@[^@\s]+\.[^@\s]+$",
        RegexOptions.Compiled,
        TimeSpan.FromMilliseconds(200));

    public static bool EsEmailValido(string? email) =>
        !string.IsNullOrWhiteSpace(email) && email.Length <= 254 && FormatoEmail.IsMatch(email);

    /// <summary>
    /// Un enum llega desde JSON como un número. .NET no valida por defecto
    /// que ese número corresponda a un valor definido del enum — un cliente
    /// puede mandar "medioPago": 99 y quedaría guardado tal cual. Este
    /// helper hace ese chequeo explícito.
    /// </summary>
    public static bool EsEnumValido<T>(T valor) where T : struct, Enum =>
        Enum.IsDefined(valor);

    public static bool EsEnumValido<T>(T? valor) where T : struct, Enum =>
        valor is null || Enum.IsDefined(valor.Value);

    // Piezas dentales válidas en notación FDI: permanentes (11-18, 21-28,
    // 31-38, 41-48) y temporales/de leche (51-55, 61-65, 71-75, 81-85).
    private static readonly int[] NumerosFdiValidos = Enumerable.Range(1, 4)
        .SelectMany(cuadrante => Enumerable.Range(1, 8).Select(posicion => cuadrante * 10 + posicion))
        .Concat(Enumerable.Range(5, 4).SelectMany(cuadrante => Enumerable.Range(1, 5).Select(posicion => cuadrante * 10 + posicion)))
        .ToArray();

    public static bool EsNumeroFdiValido(int? numeroFdi) =>
        numeroFdi is null || NumerosFdiValidos.Contains(numeroFdi.Value);
}
