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

        var tenant = await _db.Tenants.FirstOrDefaultAsync(t => t.Id == tenantId);
        if (tenant is null) return;

        if (TenantEstadoService.ActualizarSiVencio(tenant))
        {
            await _db.SaveChangesAsync();
        }

        if (tenant.Estado == TenantEstado.Activo)
        {
            context.Succeed(requirement);
        }
        // Si no, no se llama a Succeed y el pipeline de autorización lo
        // resuelve como 403 Forbidden automáticamente.
    }
}
