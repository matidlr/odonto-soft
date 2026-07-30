using Odonto.Domain.Common;
using Odonto.Domain.Entities;

namespace Odonto.Application.Agenda;

/// <summary>
/// Calcula qué recordatorios corresponden para un turno recién creado:
/// siempre 2hs antes (los dos canales), y además 24hs antes si se reservó
/// con más de una semana de anticipación (comparando por día calendario).
/// Se usa tanto desde la reserva pública (paciente) como desde la reserva
/// que hace el odontólogo/recepción para el mismo paciente.
/// </summary>
public static class RecordatorioScheduler
{
    public static List<Notificacion> Generar(Turno turno)
    {
        var diasDeAnticipacion = (turno.FechaHora.Date - turno.FechaCreacion.Date).TotalDays;
        var notificaciones = new List<Notificacion>();

        foreach (var canal in new[] { CanalNotificacion.Email, CanalNotificacion.WhatsApp })
        {
            notificaciones.Add(new Notificacion
            {
                TurnoId = turno.Id,
                Canal = canal,
                TipoRecordatorio = TipoRecordatorio.H2,
                FechaEnvioProgramada = turno.FechaHora.AddHours(-2)
            });

            if (diasDeAnticipacion > 7)
            {
                notificaciones.Add(new Notificacion
                {
                    TurnoId = turno.Id,
                    Canal = canal,
                    TipoRecordatorio = TipoRecordatorio.H24,
                    FechaEnvioProgramada = turno.FechaHora.AddHours(-24)
                });
            }
        }

        return notificaciones;
    }
}
