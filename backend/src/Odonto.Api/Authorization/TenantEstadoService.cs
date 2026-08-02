using Odonto.Domain.Common;
using Odonto.Domain.Entities;

namespace Odonto.Api.Authorization;

/// <summary>
/// Lógica compartida para decidir si un tenant "Activo" en la base todavía
/// tiene derecho a usar el sistema (está en su mes de prueba, o tiene una
/// suscripción de Mercado Pago pagando) o si hay que suspenderlo. No hay
/// ningún proceso en segundo plano que revise esto solo: se evalúa "al
/// vuelo" cada vez que alguien usa el sistema (TenantActivoHandler) o
/// consulta su propio estado (mi-tenant), y si corresponde, se persiste el
/// cambio ahí mismo.
/// </summary>
public static class TenantEstadoService
{
    public static bool EstaEnPrueba(Tenant tenant) =>
        tenant.FechaFinPrueba is DateTime fin && fin >= DateTime.UtcNow;

    public static bool TieneAccesoValido(Tenant tenant) =>
        EstaEnPrueba(tenant) || tenant.TienePagoActivo;

    /// <summary>
    /// Si el tenant figura Activo pero ya no tiene acceso válido (se venció
    /// la prueba y no hay pago), lo pasa a Suspendido en memoria. Devuelve
    /// true si tocó algo, para que el caller decida si hace SaveChanges.
    /// </summary>
    public static bool ActualizarSiVencio(Tenant tenant)
    {
        if (tenant.Estado != TenantEstado.Activo) return false;
        if (TieneAccesoValido(tenant)) return false;

        tenant.Estado = TenantEstado.Suspendido;
        return true;
    }
}
