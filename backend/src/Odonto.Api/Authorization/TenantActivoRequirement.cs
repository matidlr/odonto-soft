using Microsoft.AspNetCore.Authorization;

namespace Odonto.Api.Authorization;

/// <summary>
/// Marca los endpoints de negocio que solo deben funcionar si el tenant
/// del usuario logueado está en estado Activo (pagó la suscripción o lo
/// habilitó el SuperAdmin). El SuperAdmin siempre pasa, porque no pertenece
/// a ningún tenant.
/// </summary>
public class TenantActivoRequirement : IAuthorizationRequirement
{
}
