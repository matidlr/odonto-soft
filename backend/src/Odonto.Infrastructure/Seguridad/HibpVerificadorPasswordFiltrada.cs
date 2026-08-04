using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;
using Odonto.Application.Common.Interfaces;

namespace Odonto.Infrastructure.Seguridad;

/// <summary>
/// Usa la API pública "Pwned Passwords" de Have I Been Pwned
/// (https://haveibeenpwned.com/API/v3#PwnedPasswords) para chequear si una
/// contraseña apareció en alguna filtración conocida.
///
/// Cómo funciona sin exponer la contraseña real (k-anonymity): se calcula
/// el SHA1 de la contraseña, se manda solo a la API el prefijo de 5
/// caracteres del hash, y la API devuelve TODOS los sufijos de hash que
/// empiezan con ese prefijo (miles de ellos) — nunca sabe cuál es la
/// contraseña real, solo que su hash empieza así.
/// </summary>
public class HibpVerificadorPasswordFiltrada : IVerificadorPasswordFiltrada
{
    private readonly HttpClient _http;
    private readonly ILogger<HibpVerificadorPasswordFiltrada> _logger;

    public HibpVerificadorPasswordFiltrada(HttpClient http, ILogger<HibpVerificadorPasswordFiltrada> logger)
    {
        _http = http;
        _http.BaseAddress = new Uri("https://api.pwnedpasswords.com/");
        _http.Timeout = TimeSpan.FromSeconds(3);
        _logger = logger;
    }

    public async Task<bool> FueFiltradaAsync(string password, CancellationToken ct = default)
    {
        try
        {
            var hashCompleto = Convert.ToHexString(SHA1.HashData(Encoding.UTF8.GetBytes(password)));
            var prefijo = hashCompleto[..5];
            var sufijoBuscado = hashCompleto[5..];

            var respuesta = await _http.GetStringAsync($"range/{prefijo}", ct);

            // Cada línea: SUFIJO:CANTIDAD_DE_VECES_QUE_APARECIO
            foreach (var linea in respuesta.Split('\n'))
            {
                var partes = linea.Split(':');
                if (partes.Length == 2 && string.Equals(partes[0].Trim(), sufijoBuscado, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }
        catch (Exception ex)
        {
            // No bloqueamos el registro/reseteo de contraseña por un problema
            // del servicio externo (caído, timeout, sin internet, etc.).
            _logger.LogWarning(ex, "No se pudo verificar la contraseña contra Have I Been Pwned (se continúa sin bloquear).");
            return false;
        }
    }
}
