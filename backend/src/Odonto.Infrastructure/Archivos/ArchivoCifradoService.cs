using System.Security.Cryptography;
using Microsoft.Extensions.Configuration;
using Odonto.Application.Common.Interfaces;

namespace Odonto.Infrastructure.Archivos;

/// <summary>
/// Cifra los archivos con AES-256-GCM antes de guardarlos en disco (y los
/// descifra al leerlos). GCM es cifrado autenticado: además de que nadie
/// pueda leer el contenido sin la clave, detecta si el archivo fue
/// modificado/corrompido en el disco.
///
/// Formato del archivo en disco: [12 bytes nonce][16 bytes tag][ciphertext].
/// La clave (32 bytes, AES-256) sale de Archivos:ClaveCifrado en base64,
/// configurada por dotnet user-secrets — nunca en appsettings.json.
/// </summary>
public class ArchivoCifradoService : IArchivoCifrado
{
    private const int TamanioNonce = 12;
    private const int TamanioTag = 16;

    private readonly byte[] _clave;

    public ArchivoCifradoService(IConfiguration configuration)
    {
        var claveBase64 = configuration["Archivos:ClaveCifrado"];
        if (string.IsNullOrWhiteSpace(claveBase64))
        {
            throw new InvalidOperationException(
                "Falta configurar Archivos:ClaveCifrado (dotnet user-secrets). " +
                "Tiene que ser una clave AES-256 de 32 bytes en base64. " +
                "Se puede generar con: openssl rand -base64 32 (o el equivalente en PowerShell, ver README).");
        }

        _clave = Convert.FromBase64String(claveBase64);
        if (_clave.Length != 32)
        {
            throw new InvalidOperationException(
                $"Archivos:ClaveCifrado tiene que decodificar a exactamente 32 bytes (AES-256); tiene {_clave.Length}.");
        }
    }

    public async Task CifrarAArchivoAsync(byte[] contenido, string rutaDestino, CancellationToken ct = default)
    {
        var nonce = RandomNumberGenerator.GetBytes(TamanioNonce);
        var tag = new byte[TamanioTag];
        var ciphertext = new byte[contenido.Length];

        using (var aes = new AesGcm(_clave, TamanioTag))
        {
            aes.Encrypt(nonce, contenido, ciphertext, tag);
        }

        await using var archivo = System.IO.File.Create(rutaDestino);
        await archivo.WriteAsync(nonce, ct);
        await archivo.WriteAsync(tag, ct);
        await archivo.WriteAsync(ciphertext, ct);
    }

    public async Task<byte[]> DescifrarDeArchivoAsync(string rutaOrigen, CancellationToken ct = default)
    {
        var completo = await System.IO.File.ReadAllBytesAsync(rutaOrigen, ct);
        if (completo.Length < TamanioNonce + TamanioTag)
            throw new InvalidOperationException($"Archivo cifrado inválido o corrupto: {rutaOrigen}");

        var nonce = completo.AsSpan(0, TamanioNonce).ToArray();
        var tag = completo.AsSpan(TamanioNonce, TamanioTag).ToArray();
        var ciphertext = completo.AsSpan(TamanioNonce + TamanioTag).ToArray();
        var contenido = new byte[ciphertext.Length];

        using var aes = new AesGcm(_clave, TamanioTag);
        aes.Decrypt(nonce, ciphertext, tag, contenido);

        return contenido;
    }
}
