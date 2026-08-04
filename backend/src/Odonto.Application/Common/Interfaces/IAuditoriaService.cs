namespace Odonto.Application.Common.Interfaces;

/// <summary>
/// Registra en RegistrosAuditoria los cambios sobre datos de un paciente
/// (cobros, turnos, presupuestos, archivos, consentimientos, datos del
/// paciente, odontograma, ficha médica, notas de evolución): quién, cuándo,
/// qué acción y, cuando corresponde, el valor anterior y el nuevo.
///
/// OJO: ninguno de los dos métodos llama a SaveChangesAsync. El registro se
/// agrega al DbContext y se guarda junto con el resto de los cambios, en la
/// misma transacción que hace el controller que lo usa.
/// </summary>
public interface IAuditoriaService
{
    /// <summary>Agrega una fila de auditoría solo si el valor realmente cambió (evita ruido).</summary>
    void RegistrarCampo(Guid tenantId, Guid pacienteId, string entidad, Guid entidadId, string accion, string campo, string? anterior, string? nuevo);

    /// <summary>Agrega una fila de auditoría para una acción sobre el registro entero (sin campo puntual), por ej. "Creado" o "Eliminado".</summary>
    void RegistrarAccion(Guid tenantId, Guid pacienteId, string entidad, Guid entidadId, string accion, string? detalle = null);
}
