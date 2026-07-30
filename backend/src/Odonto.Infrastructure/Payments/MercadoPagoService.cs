using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Configuration;

namespace Odonto.Infrastructure.Payments;

/// <summary>
/// Habla con la API de Suscripciones de Mercado Pago (/preapproval).
/// Crea la suscripción en estado "pending" (sin medio de pago todavía) y
/// devuelve el init_point al que se redirige al odontólogo para que pague.
/// El estado real después se confirma vía webhook + GET /preapproval/{id}.
/// </summary>
public class MercadoPagoService
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;

    public MercadoPagoService(HttpClient httpClient, IConfiguration configuration)
    {
        _httpClient = httpClient;
        _configuration = configuration;
        _httpClient.BaseAddress = new Uri("https://api.mercadopago.com/");
    }

    private void AgregarAuth(HttpRequestMessage request)
    {
        var accessToken = _configuration["MercadoPago:AccessToken"];
        if (string.IsNullOrWhiteSpace(accessToken))
            throw new InvalidOperationException("Falta configurar MercadoPago:AccessToken (dotnet user-secrets).");

        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
    }

    public async Task<(string PreapprovalId, string InitPoint)> CrearSuscripcionAsync(
        Guid tenantId, string payerEmail, string reason, CancellationToken ct = default)
    {
        var backUrl = _configuration["MercadoPago:BackUrl"] ?? "https://www.mercadopago.com.ar";
        var monto = decimal.Parse(_configuration["MercadoPago:MontoMensual"] ?? "15000");
        var moneda = _configuration["MercadoPago:Moneda"] ?? "ARS";

        var payload = new
        {
            reason,
            external_reference = tenantId.ToString(),
            payer_email = payerEmail,
            auto_recurring = new
            {
                frequency = 1,
                frequency_type = "months",
                transaction_amount = monto,
                currency_id = moneda
            },
            back_url = backUrl,
            status = "pending"
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, "preapproval")
        {
            Content = JsonContent.Create(payload)
        };
        AgregarAuth(request);

        var response = await _httpClient.SendAsync(request, ct);
        var body = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
        {
            var xRequestId = response.Headers.TryGetValues("x-request-id", out var vals) ? vals.FirstOrDefault() : null;
            throw new InvalidOperationException(
                $"Mercado Pago devolvió {(int)response.StatusCode}: {body} (x-request-id: {xRequestId})");
        }

        using var doc = JsonDocument.Parse(body);
        var id = doc.RootElement.GetProperty("id").GetString()!;
        var initPoint = doc.RootElement.GetProperty("init_point").GetString()!;

        return (id, initPoint);
    }

    public async Task<string> ObtenerEstadoAsync(string preapprovalId, CancellationToken ct = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"preapproval/{preapprovalId}");
        AgregarAuth(request);

        var response = await _httpClient.SendAsync(request, ct);
        var body = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"Mercado Pago devolvió {(int)response.StatusCode}: {body}");

        using var doc = JsonDocument.Parse(body);
        return doc.RootElement.GetProperty("status").GetString()!;
    }
}
