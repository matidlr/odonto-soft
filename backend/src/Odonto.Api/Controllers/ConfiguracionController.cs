using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Odonto.Infrastructure.Persistence;

namespace Odonto.Api.Controllers;

/// <summary>
/// Configuración general de la clínica: datos de contacto y logo. Horarios
/// (Disponibilidad), duración de turnos (Tipos de tratamiento) y colores
/// por odontólogo (Odontólogos) ya se configuran en sus propias pantallas;
/// esto es lo que faltaba: los datos propios de la clínica.
/// </summary>
[ApiController]
[Route("api/configuracion")]
[Authorize(Policy = "TenantActivo")]
public class ConfiguracionController : ControllerBase
{
    private const long TamanioMaximoLogoBytes = 3 * 1024 * 1024; // 3 MB

    private static readonly Dictionary<string, string> TiposPermitidos = new(StringComparer.OrdinalIgnoreCase)
    {
        [".jpg"] = "image/jpeg",
        [".jpeg"] = "image/jpeg",
        [".png"] = "image/png",
        [".webp"] = "image/webp",
        [".svg"] = "image/svg+xml"
    };

    private readonly AppDbContext _db;
    private readonly IWebHostEnvironment _env;

    public ConfiguracionController(AppDbContext db, IWebHostEnvironment env)
    {
        _db = db;
        _env = env;
    }

    private Guid? TenantIdActual()
    {
        var claim = User.FindFirst("tenant_id")?.Value;
        return Guid.TryParse(claim, out var id) ? id : null;
    }

    public record ConfiguracionResponse(string Nombre, string? Direccion, string? Telefono, string? EmailContacto, bool TieneLogo);

    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct)
    {
        var tenantId = TenantIdActual();
        if (tenantId is null) return BadRequest(new { message = "No se pudo determinar el tenant del usuario." });

        var tenant = await _db.Tenants.FirstOrDefaultAsync(t => t.Id == tenantId, ct);
        if (tenant is null) return NotFound();

        return Ok(new ConfiguracionResponse(tenant.Nombre, tenant.Direccion, tenant.Telefono, tenant.EmailContacto, tenant.LogoRutaEnDisco != null));
    }

    public record EditarConfiguracionRequest(string Nombre, string? Direccion, string? Telefono, string? EmailContacto);

    [HttpPut]
    public async Task<IActionResult> Editar(EditarConfiguracionRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Nombre))
            return BadRequest(new { message = "El nombre de la clínica es obligatorio." });

        var tenantId = TenantIdActual();
        if (tenantId is null) return BadRequest(new { message = "No se pudo determinar el tenant del usuario." });

        var tenant = await _db.Tenants.FirstOrDefaultAsync(t => t.Id == tenantId, ct);
        if (tenant is null) return NotFound();

        tenant.Nombre = request.Nombre;
        tenant.Direccion = request.Direccion;
        tenant.Telefono = request.Telefono;
        tenant.EmailContacto = request.EmailContacto;

        await _db.SaveChangesAsync(ct);

        return Ok(new ConfiguracionResponse(tenant.Nombre, tenant.Direccion, tenant.Telefono, tenant.EmailContacto, tenant.LogoRutaEnDisco != null));
    }

    [HttpPost("logo")]
    [RequestSizeLimit(TamanioMaximoLogoBytes)]
    public async Task<IActionResult> SubirLogo(IFormFile archivo, CancellationToken ct)
    {
        if (archivo is null || archivo.Length == 0)
            return BadRequest(new { message = "No se recibió ningún archivo." });
        if (archivo.Length > TamanioMaximoLogoBytes)
            return BadRequest(new { message = "El logo supera el tamaño máximo permitido (3 MB)." });

        var extension = Path.GetExtension(archivo.FileName);
        if (!TiposPermitidos.TryGetValue(extension, out var contentTypeSeguro))
        {
            return BadRequest(new { message = "Tipo de archivo no permitido. Solo se aceptan imágenes (JPG, PNG, WEBP, SVG)." });
        }

        var tenantId = TenantIdActual();
        if (tenantId is null) return BadRequest(new { message = "No se pudo determinar el tenant del usuario." });

        var tenant = await _db.Tenants.FirstOrDefaultAsync(t => t.Id == tenantId, ct);
        if (tenant is null) return NotFound();

        var carpeta = Path.Combine(_env.ContentRootPath, "uploads", tenantId.Value.ToString(), "logo");
        Directory.CreateDirectory(carpeta);

        // Un solo logo por clínica: se pisa el archivo anterior si existía.
        if (tenant.LogoRutaEnDisco is not null && System.IO.File.Exists(tenant.LogoRutaEnDisco))
        {
            System.IO.File.Delete(tenant.LogoRutaEnDisco);
        }

        var nombreEnDisco = $"logo{extension}";
        var rutaCompleta = Path.Combine(carpeta, nombreEnDisco);

        await using (var stream = System.IO.File.Create(rutaCompleta))
        {
            await archivo.CopyToAsync(stream, ct);
        }

        tenant.LogoRutaEnDisco = rutaCompleta;
        tenant.LogoContentType = contentTypeSeguro;
        await _db.SaveChangesAsync(ct);

        return Ok(new { message = "Logo actualizado." });
    }

    [HttpGet("logo")]
    public async Task<IActionResult> GetLogo(CancellationToken ct)
    {
        var tenantId = TenantIdActual();
        if (tenantId is null) return NotFound();

        var tenant = await _db.Tenants.FirstOrDefaultAsync(t => t.Id == tenantId, ct);
        if (tenant?.LogoRutaEnDisco is null || tenant.LogoContentType is null)
            return NotFound();

        if (!System.IO.File.Exists(tenant.LogoRutaEnDisco))
            return NotFound(new { message = "El logo ya no está disponible en el servidor." });

        var stream = System.IO.File.OpenRead(tenant.LogoRutaEnDisco);
        return File(stream, tenant.LogoContentType);
    }
}
