using Odonto.Domain.Common;

namespace Odonto.Domain.Entities;

/// <summary>
/// Una línea de un presupuesto: un tratamiento con su precio. NumeroFdi y
/// EstadoDienteResultante son opcionales — solo se completan cuando el ítem
/// apunta a un diente puntual (para poder generar el evento del odontograma
/// al convertir el presupuesto). Si quedan vacíos, es un tratamiento general
/// (limpieza, blanqueamiento, etc.) que no toca el odontograma.
/// </summary>
public class ItemPresupuesto
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid PresupuestoId { get; set; }
    public Presupuesto Presupuesto { get; set; } = null!;

    public Guid? TipoTratamientoId { get; set; }
    public TipoTratamiento? TipoTratamiento { get; set; }

    public string Descripcion { get; set; } = string.Empty;

    public int? NumeroFdi { get; set; }
    public EstadoDiente? EstadoDienteResultante { get; set; }

    public int Cantidad { get; set; } = 1;
    public decimal PrecioUnitario { get; set; }
}
