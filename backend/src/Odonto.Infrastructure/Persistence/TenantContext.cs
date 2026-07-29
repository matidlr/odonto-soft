using Microsoft.AspNetCore.Http;
using Odonto.Application.Common.Interfaces;
using Odonto.Domain.Common;

namespace Odonto.Infrastructure.Persistence;

/// <summary>
/// Implementación real de ITenantContext: lee los claims "tenant_id" y "rol"
/// del JWT del request actual. Se registra como Scoped (vale una vez por request).
/// </summary>
public class TenantContext : ITenantContext
{
    public Guid? TenantId { get; }
    public bool EsSuperAdmin { get; }

    public TenantContext(IHttpContextAccessor httpContextAccessor)
    {
        var user = httpContextAccessor.HttpContext?.User;
        if (user is null || user.Identity?.IsAuthenticated != true)
        {
            return;
        }

        var rolClaim = user.FindFirst("rol")?.Value;
        EsSuperAdmin = rolClaim == nameof(Rol.SuperAdmin);

        var tenantClaim = user.FindFirst("tenant_id")?.Value;
        if (Guid.TryParse(tenantClaim, out var tenantId))
        {
            TenantId = tenantId;
        }
    }
}
