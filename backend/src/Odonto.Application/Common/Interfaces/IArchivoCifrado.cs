namespace Odonto.Application.Common.Interfaces;

/// <summary>
/// Cifra/descifra archivos sensibles (radiografías, PDFs, documentos) antes
/// de que toquen el disco. Existe porque los archivos hoy se guardan en el
/// disco local del servidor (no en un storage cloud que ya cifre en reposo
/// por su cuenta) — ver el checklist de seguridad, ítem "Cifrado".
/// </summary>
public interface IArchivoCifrado
{
    /// <summary>Cifra los bytes en memoria y los escribe directo en <paramref name="rutaDestino"/>.</summary>
    Task CifrarAArchivoAsync(byte[] contenido, string rutaDestino, CancellationToken ct = default);

    /// <summary>Lee el archivo cifrado de disco y devuelve el contenido original descifrado.</summary>
    Task<byte[]> DescifrarDeArchivoAsync(string rutaOrigen, CancellationToken ct = default);
}
