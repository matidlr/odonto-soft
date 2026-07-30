using System.Net.Http.Json;
using Microsoft.Extensions.Configuration;
using Odonto.Application.Common.Interfaces;

namespace Odonto.Infrastructure.Notificaciones;

/// <summary>
/// Envía emails transaccionales a través de la API REST de Brevo
/// (https://api.brevo.com/v3/smtp/email). Requiere Brevo:ApiKey y
/// Brevo:SenderEmail configurados (vía dotnet user-secrets en desarrollo).
/// </summary>
public class BrevoEmailSender : IEmailSender
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;

    public BrevoEmailSender(HttpClient httpClient, IConfiguration configuration)
    {
        _httpClient = httpClient;
        _configuration = configuration;
    }

    public async Task EnviarAsync(
        string destinatarioEmail,
        string? destinatarioNombre,
        string asunto,
        string htmlContent,
        CancellationToken ct = default)
    {
        var apiKey = _configuration["Brevo:ApiKey"];
        var senderEmail = _configuration["Brevo:SenderEmail"];
        var senderNombre = _configuration["Brevo:SenderNombre"] ?? "Odonto SaaS";

        if (string.IsNullOrWhiteSpace(apiKey) || string.IsNullOrWhiteSpace(senderEmail))
        {
            throw new InvalidOperationException(
                "Falta configurar Brevo:ApiKey y/o Brevo:SenderEmail (dotnet user-secrets).");
        }

        var payload = new
        {
            sender = new { email = senderEmail, name = senderNombre },
            to = new[] { new { email = destinatarioEmail, name = destinatarioNombre } },
            subject = asunto,
            htmlContent
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.brevo.com/v3/smtp/email")
        {
            Content = JsonContent.Create(payload)
        };
        request.Headers.Add("api-key", apiKey);

        var response = await _httpClient.SendAsync(request, ct);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(ct);
            throw new InvalidOperationException($"Brevo devolvió {(int)response.StatusCode}: {body}");
        }
    }
}
