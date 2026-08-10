using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Odonto.Domain.Entities;
using Odonto.Infrastructure.Persistence;

namespace Odonto.Api.Controllers;

/// <summary>
/// Odontólogos del tenant actual. El primero se crea junto con el Owner al
/// registrar la clínica (con login propio); los siguientes se agregan acá
/// como perfiles de datos nada más — la clínica tiene un solo login
/// compartido, y el selector de "qué odontólogo estoy usando" en el
/// frontend elige entre estos perfiles.
/// </summary>
[ApiController]
[Route("api/v1/odontologos")]
[Authorize(Policy = "TenantActivo")]
public class OdontologosController : ControllerBase
{
    private readonly AppDbContext _db;

    public OdontologosController(AppDbContext db)
    {
        _db = db;
    }

    /// <summary>Reglas comunes a Crear/Editar: no confiar en la validación de Angular.</summary>
    private static string? ValidarDatosOdontologo(string nombre, string matricula, string? especialidad, string? colorAgenda)
    {
        if (string.IsNullOrWhiteSpace(nombre) || nombre.Length > 200)
            return "El nombre es obligatorio y no puede superar los 200 caracteres.";
        if (string.IsNullOrWhiteSpace(matricula) || matricula.Length > 50)
            return "La matrícula es obligatoria y no puede superar los 50 caracteres.";
        if (especialidad?.Length > 150)
            return "La especialidad es demasiado larga.";
        if (!string.IsNullOrWhiteSpace(colorAgenda) && !System.Text.RegularExpressions.Regex.IsMatch(colorAgenda, "^#[0-9a-fA-F]{6}$",
                System.Text.RegularExpressions.RegexOptions.None, TimeSpan.FromSeconds(1)))
            return "El color de agenda tiene que ser un color hexadecimal (ej: #2563eb).";
        return null;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken ct)
    {
        var odontologos = await _db.Odontologos
            .OrderBy(o => o.Nombre)
            .Select(o => new
            {
                o.Id,
                o.Nombre,
                o.Matricula,
                o.Especialidad,
                o.ColorAgenda
            })
            .ToListAsync(ct);

        return Ok(odontologos);
    }

    public record CrearOdontologoRequest(string Nombre, string Matricula, string? Especialidad, string? ColorAgenda);

    [HttpPost]
    public async Task<IActionResult> Crear(CrearOdontologoRequest request, CancellationToken ct)
    {
        var errorCrear = ValidarDatosOdontologo(request.Nombre, request.Matricula, request.Especialidad, request.ColorAgenda);
        if (errorCrear is not null) return BadRequest(new { message = errorCrear });

        var tenantIdClaim = User.FindFirst("tenant_id")?.Value;
        if (!Guid.TryParse(tenantIdClaim, out var tenantId))
            return BadRequest(new { message = "No se pudo determinar el tenant del usuario." });

        var tenant = await _db.Tenants.Include(t => t.Plan).FirstOrDefaultAsync(t => t.Id == tenantId, ct);
        if (tenant is null) return BadRequest(new { message = "No se encontró la clínica del usuario." });

        // Sin plan asignado (no debería pasar, pero por las dudas) se trata
        // como el límite más restrictivo: 1 odontólogo.
        var maxOdontologos = tenant.Plan?.MaxOdontologos ?? 1;
        var cantidadActual = await _db.Odontologos.CountAsync(o => o.TenantId == tenantId, ct);

        if (cantidadActual >= maxOdontologos)
        {
            return BadRequest(new
            {
                message = $"Tu plan ({tenant.Plan?.Nombre ?? "sin definir"}) permite hasta {maxOdontologos} odontólogo(s). Para agregar más, pedile al SuperAdmin que suba tu plan."
            });
        }

        var odontologo = new Odontologo
        {
            TenantId = tenantId,
            Nombre = request.Nombre,
            Matricula = request.Matricula,
            Especialidad = request.Especialidad,
            ColorAgenda = string.IsNullOrWhiteSpace(request.ColorAgenda) ? "#2563eb" : request.ColorAgenda
        };

        _db.Odontologos.Add(odontologo);

        // Igual que al registrar la clínica: todo odontólogo arranca con
        // una sede Principal automática.
        _db.Sedes.Add(new Sede
        {
            TenantId = tenantId,
            OdontologoId = odontologo.Id,
            Nombre = "Principal",
            EsPrincipal = true
        });

        await _db.SaveChangesAsync(ct);

        return Ok(new { odontologo.Id });
    }

    public record EditarOdontologoRequest(string Nombre, string Matricula, string? Especialidad, string? ColorAgenda);

    [HttpPut("{id}")]
    public async Task<IActionResult> Editar(Guid id, EditarOdontologoRequest request, CancellationToken ct)
    {
        var errorEditar = ValidarDatosOdontologo(request.Nombre, request.Matricula, request.Especialidad, request.ColorAgenda);
        if (errorEditar is not null) return BadRequest(new { message = errorEditar });

        var odontologo = await _db.Odontologos.FirstOrDefaultAsync(o => o.Id == id, ct);
        if (odontologo is null) return NotFound(new { message = "Odontólogo no encontrado." });

        odontologo.Nombre = request.Nombre;
        odontologo.Matricula = request.Matricula;
        odontologo.Especialidad = request.Especialidad;
        odontologo.ColorAgenda = string.IsNullOrWhiteSpace(request.ColorAgenda) ? odontologo.ColorAgenda : request.ColorAgenda;

        await _db.SaveChangesAsync(ct);

        return Ok(new { odontologo.Id });
    }
}
