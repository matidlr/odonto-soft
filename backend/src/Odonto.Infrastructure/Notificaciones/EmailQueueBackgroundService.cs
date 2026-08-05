using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Odonto.Application.Common.Interfaces;

namespace Odonto.Infrastructure.Notificaciones;

/// <summary>
/// Lee de EmailQueue y manda cada email con un IEmailSender de un scope
/// nuevo (nunca el del request que lo encoló, que ya terminó y puede estar
/// disponed). Corre en paralelo a RecordatorioBackgroundService: ese
/// procesa recordatorios de turno por polling cada 1 minuto, este procesa
/// avisos puntuales de Auth (dispositivo nuevo, reseteo de contraseña) tan
/// pronto como se encolan.
/// </summary>
public class EmailQueueBackgroundService : BackgroundService
{
    private readonly EmailQueue _cola;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<EmailQueueBackgroundService> _logger;

    public EmailQueueBackgroundService(EmailQueue cola, IServiceScopeFactory scopeFactory, ILogger<EmailQueueBackgroundService> logger)
    {
        _cola = cola;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var email in _cola.Reader.ReadAllAsync(stoppingToken))
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var emailSender = scope.ServiceProvider.GetRequiredService<IEmailSender>();
                await emailSender.EnviarAsync(email.DestinatarioEmail, email.DestinatarioNombre, email.Asunto, email.HtmlContent, stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex, "No se pudo enviar el email encolado \"{Asunto}\" a {Email}.", email.Asunto, EnmascararEmail(email.DestinatarioEmail));
            }
        }
    }

    // Versión mínima del enmascarado que ya usa Odonto.Api.Logging.SaneadorLogs
    // (no se puede referenciar desde acá: Infrastructure no depende de Api).
    // Mismo motivo: no dejar el email completo en el archivo de log.
    private static string EnmascararEmail(string? email)
    {
        if (string.IsNullOrWhiteSpace(email)) return "(vacío)";
        var arroba = email.IndexOf('@');
        if (arroba <= 0) return "***";
        var visible = arroba <= 2 ? email[..1] : email[..2];
        return $"{visible}***{email[arroba..]}";
    }
}
