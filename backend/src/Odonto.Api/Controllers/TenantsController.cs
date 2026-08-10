using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Odonto.Api.Authorization;
using Odonto.Domain.Common;
using Odonto.Infrastructure.Persistence;

namespace Odonto.Api.Controllers;

/// <summary>
/// Gestión de tenants: listar y activar/suspender es solo para el SuperAdmin.
/// "mi-tenant" es para cualquier usuario logueado, así el frontend puede
/// mostrar el estado (pendiente/activo/suspendido) de su propia cuenta.
/// </summary>
[ApiController]
[Route("api/v1/tenants")]
public class TenantsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ILogger<TenantsController> _logger;

    public TenantsController(AppDbContext db, ILogger<TenantsController> logger)
    {
        _db = db;
        _logger = logger;
    }

    // Devuelve el UsuarioId (no el email): esto solo se usa para loguear
    // quién hizo la acción, y un email es información personal que no debe
    // terminar en un archivo de log. El GUID alcanza para auditar y, si
    // hace falta, se puede buscar el email en la base a partir de él.
    private string IdentificacionDelSuperAdmin()
    {
        return User.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)?.Value ?? "SuperAdmin desconocido";
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
                t.FechaAlta,
                t.PlanId,
                PlanNombre = t.Plan != null ? t.Plan.Nombre : null,
                MaxOdontologos = t.Plan != null ? t.Plan.MaxOdontologos : (int?)null,
                CantidadOdontologos = _db.Odontologos.Count(o => o.TenantId == t.Id),
                t.FechaFinPrueba,
                t.TienePagoActivo
            })
            .ToListAsync(ct);

        return Ok(tenants);
    }

    // Catálogo de planes activos: lo usa tanto el selector del SuperAdmin
    // como la pantalla de "Plan" de cada clínica (para elegir a cuál
    // suscribirse), así que cualquier usuario logueado puede consultarlo.
    [HttpGet("/api/v1/planes")]
    [Authorize]
    public async Task<IActionResult> GetPlanes(CancellationToken ct)
    {
        var planes = await _db.Planes
            .Where(p => p.Activo)
            .OrderBy(p => p.Orden)
            .Select(p => new { p.Id, p.Nombre, p.MaxOdontologos, p.PrecioMensual })
            .ToListAsync(ct);

        return Ok(planes);
    }

    public record CambiarPlanRequest(Guid PlanId);

    [HttpPut("{id}/plan")]
    [Authorize(Roles = "SuperAdmin")]
    public async Task<IActionResult> CambiarPlan(Guid id, CambiarPlanRequest request, CancellationToken ct)
    {
        var tenant = await _db.Tenants.FindAsync(new object[] { id }, ct);
        if (tenant is null) return NotFound();

        var plan = await _db.Planes.FirstOrDefaultAsync(p => p.Id == request.PlanId, ct);
        if (plan is null) return BadRequest(new { message = "Plan inválido." });

        // No dejamos bajar a un plan que ya no le alcanza para los
        // odontólogos que la clínica tiene cargados hoy.
        var cantidadActual = await _db.Odontologos.CountAsync(o => o.TenantId == id, ct);
        if (cantidadActual > plan.MaxOdontologos)
        {
            return BadRequest(new
            {
                message = $"Esta clínica tiene {cantidadActual} odontólogo(s) cargados y el plan {plan.Nombre} solo permite {plan.MaxOdontologos}. Tendrían que dar de baja alguno primero."
            });
        }

        tenant.PlanId = plan.Id;
        await _db.SaveChangesAsync(ct);

        _logger.LogWarning("SuperAdmin {SuperAdmin} cambió el plan del tenant {TenantId} a {Plan}",
            IdentificacionDelSuperAdmin(), tenant.Id, plan.Nombre);

        return Ok(new { tenant.Id, PlanId = plan.Id, PlanNombre = plan.Nombre });
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

        var tenant = await _db.Tenants.Include(t => t.Plan).FirstOrDefaultAsync(t => t.Id == tenantId, ct);
        if (tenant is null) return NotFound();

        if (TenantEstadoService.ActualizarSiVencio(tenant))
        {
            await _db.SaveChangesAsync(ct);
        }

        var enPrueba = TenantEstadoService.EstaEnPrueba(tenant);
        var diasRestantesDePrueba = enPrueba
            ? Math.Max(0, (int)Math.Ceiling((tenant.FechaFinPrueba!.Value - DateTime.UtcNow).TotalDays))
            : 0;

        return Ok(new
        {
            tenant.Id,
            tenant.Nombre,
            tenant.Slug,
            Estado = tenant.Estado.ToString(),
            EnPrueba = enPrueba,
            DiasRestantesDePrueba = diasRestantesDePrueba,
            tenant.TienePagoActivo,
            PlanId = tenant.PlanId,
            PlanNombre = tenant.Plan != null ? tenant.Plan.Nombre : null
        });
    }

    [HttpPut("{id}/activar")]
    [Authorize(Roles = "SuperAdmin")]
    public async Task<IActionResult> Activar(Guid id, CancellationToken ct)
    {
        var tenant = await _db.Tenants.FindAsync(new object[] { id }, ct);
        if (tenant is null) return NotFound();

        tenant.Estado = TenantEstado.Activo;
        await _db.SaveChangesAsync(ct);

        _logger.LogWarning("SuperAdmin {SuperAdmin} activó el tenant {TenantId}", IdentificacionDelSuperAdmin(), tenant.Id);

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

        _logger.LogWarning("SuperAdmin {SuperAdmin} suspendió el tenant {TenantId}", IdentificacionDelSuperAdmin(), tenant.Id);

        return Ok(new { tenant.Id, Estado = tenant.Estado.ToString() });
    }
}
