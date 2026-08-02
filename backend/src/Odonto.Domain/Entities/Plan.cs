namespace Odonto.Domain.Entities;

/// <summary>
/// Catálogo de planes de suscripción. No es tenant-scoped: es compartido
/// por toda la plataforma y lo gestiona el SuperAdmin. Define cuántos
/// odontólogos puede tener una clínica y (a futuro, cuando se resuelva la
/// integración con Mercado Pago) cuánto se le cobra por mes.
/// </summary>
public class Plan
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Nombre { get; set; } = string.Empty;
    public int MaxOdontologos { get; set; }
    public decimal PrecioMensual { get; set; }

    // Para poder "descontinuar" un plan sin borrarlo (clínicas viejas
    // podrían seguir usándolo aunque ya no se ofrezca a nuevos clientes).
    public bool Activo { get; set; } = true;

    // Orden de despliegue en listados (de más barato a más caro).
    public int Orden { get; set; }
}
