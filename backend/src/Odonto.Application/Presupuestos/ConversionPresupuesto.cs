using Odonto.Domain.Common;
using Odonto.Domain.Entities;

namespace Odonto.Application.Presupuestos;

/// <summary>
/// Regla de negocio de "convertir un presupuesto en tratamiento": por cada
/// ítem del presupuesto que tiene diente (NumeroFdi) y estado resultante
/// asignados, genera un EventoOdontograma Planificado. Función pura, sin
/// acceso a datos — guardar los eventos y marcar el presupuesto como
/// convertido es responsabilidad de quien la llama (el controller), igual
/// que RecordatorioScheduler para los turnos.
/// </summary>
public static class ConversionPresupuesto
{
    public static List<EventoOdontograma> GenerarEventos(Presupuesto presupuesto, DateTime fecha)
    {
        var eventos = new List<EventoOdontograma>();

        foreach (var item in presupuesto.Items)
        {
            if (item.NumeroFdi is int numeroFdi && item.EstadoDienteResultante is EstadoDiente estado)
            {
                eventos.Add(new EventoOdontograma
                {
                    TenantId = presupuesto.TenantId,
                    PacienteId = presupuesto.PacienteId,
                    NumeroFdi = numeroFdi,
                    Estado = estado,
                    EstadoTratamiento = EstadoTratamiento.Planificado,
                    Tratamiento = item.Descripcion,
                    Nota = "Generado desde presupuesto aprobado.",
                    OdontologoId = presupuesto.OdontologoId,
                    Fecha = fecha
                });
            }
        }

        return eventos;
    }
}
