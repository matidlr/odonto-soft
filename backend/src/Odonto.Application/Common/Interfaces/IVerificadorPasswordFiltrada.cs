namespace Odonto.Application.Common.Interfaces;

/// <summary>
/// Consulta si una contraseña apareció en alguna filtración de datos
/// conocida (ej. Have I Been Pwned). Nunca manda la contraseña real a
/// ningún lado — solo un prefijo de su hash (k-anonymity).
/// </summary>
public interface IVerificadorPasswordFiltrada
{
    /// <summary>
    /// Devuelve true si la contraseña es conocida como filtrada. Si el
    /// servicio externo falla o no responde, devuelve false (no bloquea al
    /// usuario por un problema de un tercero, no por la contraseña en sí).
    /// </summary>
    Task<bool> FueFiltradaAsync(string password, CancellationToken ct = default);
}
