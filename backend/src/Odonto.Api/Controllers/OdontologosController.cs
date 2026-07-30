using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Odonto.Infrastructure.Persistence;

namespace Odonto.Api.Controllers;

/// <summary>
/// Odontólogos del tenant actual. Se usa para llenar selects en el
/// frontend (agenda, disponibilidad) — no hace falta un CRUD completo
/// todavía, el alta de odontólogos nuevos en una clínica con varios
/// profesionales queda para más adelante.
/// </summary>
[ApiController]
[Route("api/odontologos")]
[Authorize(Policy = "TenantActivo")]
public class OdontologosController : ControllerBase
{
    private readonly AppDbContext _db;

    public OdontologosController(AppDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken ct)
    {
        var odontologos = await _db.Odontologos
            .Select(o => new
            {
                o.Id,
                Nombre = o.Usuario.Nombre,
                o.Matricula,
                o.Especialidad,
                o.ColorAgenda
            })
            .ToListAsync(ct);

        return Ok(odontologos);
    }
}
