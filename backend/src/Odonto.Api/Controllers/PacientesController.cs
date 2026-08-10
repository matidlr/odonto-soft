using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Odonto.Api.Validacion;
using Odonto.Application.Common.Interfaces;
using Odonto.Domain.Entities;
using Odonto.Infrastructure.Persistence;

namespace Odonto.Api.Controllers;

/// <summary>
/// Pacientes vistos/gestionados por el odontólogo o la recepción.
/// El alta pública (vía link, sin login) vive en PublicController; este
/// POST es para cuando el consultorio carga al paciente directamente
/// (por ejemplo, alguien que llama por teléfono o no usa el link solo).
/// </summary>
[ApiController]
[Route("api/v1/pacientes")]
[Authorize(Policy = "TenantActivo")]
public class PacientesController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ILogger<PacientesController> _logger;
    private readonly IAuditoriaService _auditoria;

    public PacientesController(AppDbContext db, ILogger<PacientesController> logger, IAuditoriaService auditoria)
    {
        _db = db;
        _logger = logger;
        _auditoria = auditoria;
    }

    private Guid? UsuarioIdActual()
    {
        var claim = User.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)?.Value;
        return Guid.TryParse(claim, out var id) ? id : null;
    }

    /// <summary>
    /// Reglas comunes a Crear/Editar: nunca confiar en que el formulario de
    /// Angular ya validó esto, porque cualquiera puede llamar a la API
    /// directamente. No valida OdontologoPrincipalId (necesita ir a la DB,
    /// se hace aparte en cada acción).
    /// </summary>
    private static string? ValidarDatosPaciente(string nombre, string? dni, string? telefono, string? email, DateTime? fechaNacimiento)
    {
        if (string.IsNullOrWhiteSpace(nombre) || nombre.Length > 200)
            return "El nombre es obligatorio y no puede superar los 200 caracteres.";
        if (dni?.Length > 30)
            return "El DNI es demasiado largo.";
        if (telefono?.Length > 30)
            return "El teléfono es demasiado largo.";
        if (!string.IsNullOrWhiteSpace(email) && !Validaciones.EsEmailValido(email))
            return "El email no tiene un formato válido.";
        if (fechaNacimiento is DateTime f && (f.Date > DateTime.UtcNow.Date || f.Year < 1900))
            return "La fecha de nacimiento no es válida.";
        return null;
    }

    // Tope de seguridad: nunca traer una lista sin límite (si una clínica
    // llega a tener miles de pacientes, esto evita una consulta gigante sin
    // querer). No es paginación real todavía — el día que una clínica se
    // acerque a este número, ahí sí hace falta paginación con búsqueda del
    // lado del servidor (hoy el buscador de la pantalla filtra en el
    // navegador sobre la lista ya cargada).
    private const int LimiteListado = 500;

    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] Guid? odontologoId,
        [FromQuery] bool incluirInactivos,
        CancellationToken ct)
    {
        var query = _db.Pacientes.AsQueryable();
        if (odontologoId is Guid oid) query = query.Where(p => p.OdontologoPrincipalId == oid);
        // Por default no mostramos los dados de baja, para no ensuciar el
        // listado normal; incluirInactivos=true los trae igual (por si
        // hace falta reactivar a alguien).
        if (!incluirInactivos) query = query.Where(p => p.Activo);

        var pacientes = await query
            .OrderBy(p => p.Nombre)
            .Take(LimiteListado)
            .Select(p => new
            {
                p.Id,
                p.Nombre,
                p.Dni,
                p.Telefono,
                p.Email,
                p.FechaNacimiento,
                p.OdontologoPrincipalId,
                p.Activo
            })
            .ToListAsync(ct);

        return Ok(pacientes);
    }

    public record CrearPacienteRequest(
        string Nombre,
        string? Dni,
        string? Telefono,
        string? Email,
        DateTime? FechaNacimiento,
        Guid? OdontologoPrincipalId);

    [HttpPost]
    public async Task<IActionResult> Crear(CrearPacienteRequest request, CancellationToken ct)
    {
        var error = ValidarDatosPaciente(request.Nombre, request.Dni, request.Telefono, request.Email, request.FechaNacimiento);
        if (error is not null) return BadRequest(new { message = error });

        if (request.OdontologoPrincipalId is Guid odontologoPrincipalId)
        {
            var existeOdontologo = await _db.Odontologos.AnyAsync(o => o.Id == odontologoPrincipalId, ct);
            if (!existeOdontologo) return BadRequest(new { message = "Odontólogo principal inválido." });
        }

        var tenantIdClaim = User.FindFirst("tenant_id")?.Value;
        if (!Guid.TryParse(tenantIdClaim, out var tenantId))
            return BadRequest(new { message = "No se pudo determinar el tenant del usuario." });

        var paciente = new Paciente
        {
            TenantId = tenantId,
            Nombre = request.Nombre,
            Dni = request.Dni,
            Telefono = request.Telefono,
            Email = request.Email,
            FechaNacimiento = request.FechaNacimiento,
            OdontologoPrincipalId = request.OdontologoPrincipalId
        };

        _db.Pacientes.Add(paciente);

        _auditoria.RegistrarAccion(tenantId, paciente.Id, "Paciente", paciente.Id, "Creado", paciente.Nombre);

        await _db.SaveChangesAsync(ct);

        return Ok(new { paciente.Id });
    }

    public record EditarPacienteRequest(
        string Nombre,
        string? Dni,
        string? Telefono,
        string? Email,
        DateTime? FechaNacimiento,
        Guid? OdontologoPrincipalId);

    [HttpPut("{id}")]
    public async Task<IActionResult> Editar(Guid id, EditarPacienteRequest request, CancellationToken ct)
    {
        var error = ValidarDatosPaciente(request.Nombre, request.Dni, request.Telefono, request.Email, request.FechaNacimiento);
        if (error is not null) return BadRequest(new { message = error });

        if (request.OdontologoPrincipalId is Guid odontologoPrincipalId)
        {
            var existeOdontologo = await _db.Odontologos.AnyAsync(o => o.Id == odontologoPrincipalId, ct);
            if (!existeOdontologo) return BadRequest(new { message = "Odontólogo principal inválido." });
        }

        var paciente = await _db.Pacientes.FirstOrDefaultAsync(p => p.Id == id, ct);
        if (paciente is null) return NotFound();

        _auditoria.RegistrarCampo(paciente.TenantId, paciente.Id, "Paciente", paciente.Id, "Editado", "Nombre", paciente.Nombre, request.Nombre);
        _auditoria.RegistrarCampo(paciente.TenantId, paciente.Id, "Paciente", paciente.Id, "Editado", "Dni", paciente.Dni, request.Dni);
        _auditoria.RegistrarCampo(paciente.TenantId, paciente.Id, "Paciente", paciente.Id, "Editado", "Telefono", paciente.Telefono, request.Telefono);
        _auditoria.RegistrarCampo(paciente.TenantId, paciente.Id, "Paciente", paciente.Id, "Editado", "Email", paciente.Email, request.Email);
        _auditoria.RegistrarCampo(paciente.TenantId, paciente.Id, "Paciente", paciente.Id, "Editado", "FechaNacimiento",
            paciente.FechaNacimiento?.ToString("yyyy-MM-dd"), request.FechaNacimiento?.ToString("yyyy-MM-dd"));
        _auditoria.RegistrarCampo(paciente.TenantId, paciente.Id, "Paciente", paciente.Id, "Editado", "OdontologoPrincipalId",
            paciente.OdontologoPrincipalId?.ToString(), request.OdontologoPrincipalId?.ToString());

        paciente.Nombre = request.Nombre;
        paciente.Dni = request.Dni;
        paciente.Telefono = request.Telefono;
        paciente.Email = request.Email;
        paciente.FechaNacimiento = request.FechaNacimiento;
        paciente.OdontologoPrincipalId = request.OdontologoPrincipalId;

        await _db.SaveChangesAsync(ct);

        return Ok(new { paciente.Id });
    }

    /// <summary>
    /// Baja lógica: el paciente deja de aparecer en los listados, pero sus
    /// datos, turnos e historia clínica quedan intactos (nunca se borran).
    /// Se puede deshacer con /reactivar.
    /// </summary>
    [HttpDelete("{id}")]
    public async Task<IActionResult> Eliminar(Guid id, CancellationToken ct)
    {
        var paciente = await _db.Pacientes.FirstOrDefaultAsync(p => p.Id == id, ct);
        if (paciente is null) return NotFound();

        paciente.Activo = false;

        _auditoria.RegistrarAccion(paciente.TenantId, paciente.Id, "Paciente", paciente.Id, "DadoDeBaja");

        await _db.SaveChangesAsync(ct);

        _logger.LogWarning("Paciente {PacienteId} dado de baja por usuario {UsuarioId}",
            paciente.Id, UsuarioIdActual());

        return Ok(new { message = "Paciente dado de baja." });
    }

    [HttpPost("{id}/reactivar")]
    public async Task<IActionResult> Reactivar(Guid id, CancellationToken ct)
    {
        var paciente = await _db.Pacientes.FirstOrDefaultAsync(p => p.Id == id, ct);
        if (paciente is null) return NotFound();

        paciente.Activo = true;

        _auditoria.RegistrarAccion(paciente.TenantId, paciente.Id, "Paciente", paciente.Id, "Reactivado");

        await _db.SaveChangesAsync(ct);

        _logger.LogWarning("Paciente {PacienteId} reactivado por usuario {UsuarioId}",
            paciente.Id, UsuarioIdActual());

        return Ok(new { message = "Paciente reactivado." });
    }
}
