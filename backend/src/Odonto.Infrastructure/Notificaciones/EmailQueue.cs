using System.Threading.Channels;
using Odonto.Application.Common.Interfaces;

namespace Odonto.Infrastructure.Notificaciones;

public record EmailPendiente(string DestinatarioEmail, string? DestinatarioNombre, string Asunto, string HtmlContent);

/// <summary>
/// Implementación de IEmailQueue con un Channel en memoria. Encolar() solo
/// escribe en el canal y vuelve al instante; EmailQueueBackgroundService es
/// quien lee del otro lado y manda los emails de verdad.
/// </summary>
public class EmailQueue : IEmailQueue
{
    private readonly Channel<EmailPendiente> _canal = Channel.CreateUnbounded<EmailPendiente>();

    public ChannelReader<EmailPendiente> Reader => _canal.Reader;

    public void Encolar(string destinatarioEmail, string? destinatarioNombre, string asunto, string htmlContent)
    {
        _canal.Writer.TryWrite(new EmailPendiente(destinatarioEmail, destinatarioNombre, asunto, htmlContent));
    }
}
