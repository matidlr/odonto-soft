using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Odonto.Domain.Common;
using Odonto.Infrastructure.Persistence;

namespace Odonto.Api.Controllers;

/// <summary>
/// Gestión de tenants: listar y activar/suspender es solo para el SuperAdmin.
/// "mi-tenant" es para cualquier usuario logueado, así el frontend puede
/// mostrar el estado (pendiente/activo/suspendido) de su propia cuenta.
/// </summary>
[ApiController]
[Route("api/tenants")]
public class TenantsController : ControllerBase
{
    private readonly AppDbContext _db;

    public TenantsController(AppDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    [Authorize(Roles = "SuperAdmin")]
    public async Task<IActionResult> GetAll(CancellationToken ct)
    {
        var tenants = await _db.Tenants
            .Select(t => new
            {
                t.Id,
                t.Nombre,
                t.Slug,
                Estado = t.Estado.ToString(),
                t.FechaAlta
            })
            .ToListAsync(ct);

        return Ok(tenants);
    }

    [HttpGet("mi-tenant")]
    [Authorize]
    public async Task<IActionResult> MiTenant(CancellationToken ct)
    {
        var tenantIdClaim = User.FindFirst("tenant_id")?.Value;
        if (!Guid.TryParse(tenantIdClaim, out var tenantId))
        {
            return NotFound(new { message = "El usuario autenticado no pertenece a ningún tenant (¿sos SuperAdmin?)." });
        }

        var tenant = await _db.Tenants
            .Where(t => t.Id == tenantId)
            .Select(t => new { t.Id, t.Nombre, t.Slug, Estado = t.Estado.ToString() })
            .FirstOrDefaultAsync(ct);

        return tenant is null ? NotFound() : Ok(tenant);
    }

    [HttpPut("{id}/activar")]
    [Authorize(Roles = "SuperAdmin")]
    public async Task<IActionResult> Activar(Guid id, CancellationToken ct)
    {
        var tenant = await _db.Tenants.FindAsync(new object[] { id }, ct);
        if (tenant is null) return NotFound();

        tenant.Estado = TenantEstado.Activo;
        await _db.SaveChangesAsync(ct);

        return Ok(new { tenant.Id, Estado = tenant.Estado.ToString() });
    }

    [HttpPut("{id}/suspender")]
    [Authorize(Roles = "SuperAdmin")]
    public async Task<IActionResult> Suspender(Guid id, CancellationToken ct)
    {
        var tenant = await _db.Tenants.FindAsync(new object[] { id }, ct);
        if (tenant is null) return NotFound();

        tenant.Estado = TenantEstado.Suspendido;
        await _db.SaveChangesAsync(ct);

        return Ok(new { tenant.Id, Estado = tenant.Estado.ToString() });
    }
}
