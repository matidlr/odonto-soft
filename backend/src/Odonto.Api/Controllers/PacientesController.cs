using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Odonto.Infrastructure.Persistence;

namespace Odonto.Api.Controllers;

/// <summary>
/// Primer endpoint "de negocio" real. Sirve para probar dos cosas a la vez:
/// 1) que el filtro global por TenantId en AppDbContext funciona solo
///    (no hace falta filtrar a mano acá).
/// 2) que la policy TenantActivo bloquea el acceso si el tenant no está activo.
/// </summary>
[ApiController]
[Route("api/pacientes")]
[Authorize(Policy = "TenantActivo")]
public class PacientesController : ControllerBase
{
    private readonly AppDbContext _db;

    public PacientesController(AppDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken ct)
    {
        var pacientes = await _db.Pacientes
            .Select(p => new { p.Id, p.Nombre, p.Dni, p.Telefono, p.Email })
            .ToListAsync(ct);

        return Ok(pacientes);
    }
}
