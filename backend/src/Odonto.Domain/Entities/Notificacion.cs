using Odonto.Domain.Common;

namespace Odonto.Domain.Entities;

/// <summary>
/// Un recordatorio programado para un turno (uno por canal y por momento
/// de aviso). El job en segundo plano (a implementar) revisa cuáles ya
/// deberían haberse enviado y los dispara.
/// </summary>
public class Notificacion
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid TurnoId { get; set; }
    public Turno Turno { get; set; } = null!;

    public CanalNotificacion Canal { get; set; }
    public TipoRecordatorio TipoRecordatorio { get; set; }

    public DateTime FechaEnvioProgramada { get; set; }
    public bool Enviado { get; set; }
    public DateTime? FechaEnvioReal { get; set; }
}
