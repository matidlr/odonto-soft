namespace Odonto.Application.Common.Interfaces;

public interface IEmailSender
{
    Task EnviarAsync(
        string destinatarioEmail,
        string? destinatarioNombre,
        string asunto,
        string htmlContent,
        CancellationToken ct = default);
}
