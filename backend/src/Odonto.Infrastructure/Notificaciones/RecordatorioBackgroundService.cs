using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Odonto.Application.Common.Interfaces;
using Odonto.Domain.Common;
using Odonto.Infrastructure.Persistence;

namespace Odonto.Infrastructure.Notificaciones;

/// <summary>
/// Revisa cada un minuto qué recordatorios de email ya deberían haberse
/// enviado (FechaEnvioProgramada &lt;= ahora, Enviado = false) y los manda.
/// Los de WhatsApp quedan guardados pero sin procesar hasta que se
/// implemente ese canal.
/// </summary>
public class RecordatorioBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<RecordatorioBackgroundService> _logger;
    private static readonly TimeSpan Intervalo = TimeSpan.FromMinutes(1);

    public RecordatorioBackgroundService(IServiceScopeFactory scopeFactory, ILogger<RecordatorioBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcesarPendientesAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error procesando recordatorios pendientes.");
            }

            await Task.Delay(Intervalo, stoppingToken);
        }
    }

    private async Task ProcesarPendientesAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var emailSender = scope.ServiceProvider.GetRequiredService<IEmailSender>();

        var ahora = DateTime.UtcNow;

        // IgnoreQueryFilters: este job corre sin usuario logueado, así que no
        // hay tenant en contexto. Tiene que ver los recordatorios de todos
        // los tenants, no solo los de uno.
        var pendientes = await db.Notificaciones
            .IgnoreQueryFilters()
            .Include(n => n.Turno).ThenInclude(t => t.Paciente)
            .Where(n => !n.Enviado
                && n.Canal == CanalNotificacion.Email
                && n.FechaEnvioProgramada <= ahora)
            .ToListAsync(ct);

        if (pendientes.Count == 0) return;

        _logger.LogInformation("Procesando {Cantidad} recordatorios pendientes.", pendientes.Count);

        foreach (var notificacion in pendientes)
        {
            var turno = notificacion.Turno;

            if (turno.Estado == TurnoEstado.Cancelado)
            {
                notificacion.Enviado = true;
                notificacion.FechaEnvioReal = ahora;
                continue;
            }

            if (string.IsNullOrWhiteSpace(turno.Paciente.Email))
            {
                notificacion.Enviado = true;
                notificacion.FechaEnvioReal = ahora;
                continue;
            }

            var esRecordatorio24 = notificacion.TipoRecordatorio == TipoRecordatorio.H24;
            var asunto = esRecordatorio24
                ? "Recordatorio: turno mañana"
                : "Recordatorio: turno en 2 horas";

            // El nombre del paciente lo carga el propio paciente (o el
            // consultorio) como texto libre — nunca hay que pegarlo directo
            // en HTML sin escapar, porque terminaría siendo parte del email.
            var nombreSeguro = System.Net.WebUtility.HtmlEncode(turno.Paciente.Nombre);

            var cuerpo =
                $"<p>Hola {nombreSeguro},</p>" +
                $"<p>Te recordamos tu turno el <strong>{turno.FechaHora:dddd dd/MM/yyyy}</strong> a las <strong>{turno.FechaHora:HH:mm}</strong>.</p>" +
                "<p>Si necesitás cancelar o reprogramar, contactanos.</p>";

            try
            {
                await emailSender.EnviarAsync(turno.Paciente.Email, turno.Paciente.Nombre, asunto, cuerpo, ct);
                notificacion.Enviado = true;
                notificacion.FechaEnvioReal = DateTime.UtcNow;
                _logger.LogInformation("Recordatorio {Id} enviado a {Email}.", notificacion.Id, turno.Paciente.Email);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "No se pudo enviar el recordatorio {Id}, se reintenta en la próxima pasada.", notificacion.Id);
            }
        }

        await db.SaveChangesAsync(ct);
    }
}
