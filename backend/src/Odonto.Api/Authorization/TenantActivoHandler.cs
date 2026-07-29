using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Odonto.Domain.Common;
using Odonto.Infrastructure.Persistence;

namespace Odonto.Api.Authorization;

public class TenantActivoHandler : AuthorizationHandler<TenantActivoRequirement>
{
    private readonly AppDbContext _db;

    public TenantActivoHandler(AppDbContext db)
    {
        _db = db;
    }

    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        TenantActivoRequirement requirement)
    {
        var rol = context.User.FindFirst("rol")?.Value;
        if (rol == nameof(Rol.SuperAdmin))
        {
            // El SuperAdmin no pertenece a ningún tenant: siempre tiene acceso.
            context.Succeed(requirement);
            return;
        }

        var tenantIdClaim = context.User.FindFirst("tenant_id")?.Value;
        if (!Guid.TryParse(tenantIdClaim, out var tenantId))
        {
            // Sin tenant_id no hay forma de saber si está activo: no se cumple.
            return;
        }

        var estado = await _db.Tenants
            .Where(t => t.Id == tenantId)
            .Select(t => (TenantEstado?)t.Estado)
            .FirstOrDefaultAsync();

        if (estado == TenantEstado.Activo)
        {
            context.Succeed(requirement);
        }
        // Si no, no se llama a Succeed y el pipeline de autorización lo
        // resuelve como 403 Forbidden automáticamente.
    }
}
