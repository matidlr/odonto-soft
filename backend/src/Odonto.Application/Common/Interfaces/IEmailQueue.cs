namespace Odonto.Application.Common.Interfaces;

/// <summary>
/// Cola en memoria para emails que no necesitan bloquear la respuesta al
/// usuario (avisos, no datos de negocio). Encolar es instantáneo: el envío
/// real lo hace un BackgroundService aparte. Si el proceso se reinicia justo
/// con algo en la cola, ese email puntual se pierde — aceptable para este
/// tipo de aviso, no para nada que el usuario dependa de recibir sí o sí.
/// </summary>
public interface IEmailQueue
{
    void Encolar(string destinatarioEmail, string? destinatarioNombre, string asunto, string htmlContent);
}
