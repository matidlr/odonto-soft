using Odonto.Domain.Common;
using Odonto.Domain.Entities;

namespace Odonto.Api.Payments;

/// <summary>
/// Traduce el "status" que devuelve la API de Mercado Pago (de un
/// preapproval) al Estado/TienePagoActivo del Tenant. La usan tanto el
/// webhook (WebhooksController, cuando MP nos avisa solo) como el endpoint
/// de sincronización manual (SuscripcionController, para no depender de que
/// el webhook llegue — útil en desarrollo local sin URL pública).
/// </summary>
public static class EstadoMercadoPagoMapper
{
    /// <summary>Aplica el estado al tenant. Devuelve true si algo cambió.</summary>
    public static bool Aplicar(Tenant tenant, string estadoMp)
    {
        var estadoAnterior = tenant.Estado;
        var pagoActivoAnterior = tenant.TienePagoActivo;

        tenant.Estado = estadoMp switch
        {
            "authorized" => TenantEstado.Activo,
            "paused" => TenantEstado.Suspendido,
            "cancelled" => TenantEstado.Suspendido,
            _ => tenant.Estado // "pending" u otro: todavía no cambiamos nada
        };

        tenant.TienePagoActivo = estadoMp == "authorized";

        return tenant.Estado != estadoAnterior || tenant.TienePagoActivo != pagoActivoAnterior;
    }
}
