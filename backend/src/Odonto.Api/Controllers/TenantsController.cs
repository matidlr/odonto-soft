using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Odonto.Infrastructure.Persistence;

namespace Odonto.Api.Controllers;

/// <summary>
/// Endpoints de ejemplo para el SuperAdmin: listar tenants y su estado
/// (pendiente / activo / suspendido). Sirve como referencia de cómo
/// se protegen los endpoints por rol una vez que esté la autenticación real.
/// </summary>
[ApiController]
[Route("api/tenants")]
[Authorize(Roles = "SuperAdmin")]
public class TenantsController : ControllerBase
{
    private readonly AppDbContext _db;

    public TenantsController(AppDbContext db)
    {
        _db = db;
    }

    [HttpGet]
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
}
