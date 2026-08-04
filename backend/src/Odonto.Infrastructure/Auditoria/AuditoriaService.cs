using Microsoft.AspNetCore.Http;
using Odonto.Application.Common.Interfaces;
using Odonto.Domain.Entities;
using Odonto.Infrastructure.Persistence;

namespace Odonto.Infrastructure.Auditoria;

public class AuditoriaService : IAuditoriaService
{
    private readonly AppDbContext _db;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public AuditoriaService(AppDbContext db, IHttpContextAccessor httpContextAccessor)
    {
        _db = db;
        _httpContextAccessor = httpContextAccessor;
    }

    // "sub" es el nombre del claim del ID de usuario en el JWT (igual que
    // JwtRegisteredClaimNames.Sub, pero sin depender de ese paquete acá en
    // Infrastructure — Api sí lo tiene referenciado, pero este proyecto no).
    private Guid? UsuarioIdActual()
    {
        var claim = _httpContextAccessor.HttpContext?.User.FindFirst("sub")?.Value;
        return Guid.TryParse(claim, out var id) ? id : null;
    }

    public void RegistrarCampo(Guid tenantId, Guid pacienteId, string entidad, Guid entidadId, string accion, string campo, string? anterior, string? nuevo)
    {
        if (anterior == nuevo) return;
        Agregar(tenantId, pacienteId, entidad, entidadId, accion, campo, anterior, nuevo);
    }

    public void RegistrarAccion(Guid tenantId, Guid pacienteId, string entidad, Guid entidadId, string accion, string? detalle = null)
    {
        Agregar(tenantId, pacienteId, entidad, entidadId, accion, campo: null, anterior: null, nuevo: detalle);
    }

    private void Agregar(Guid tenantId, Guid pacienteId, string entidad, Guid entidadId, string accion, string? campo, string? anterior, string? nuevo)
    {
        _db.RegistrosAuditoria.Add(new RegistroAuditoria
        {
            TenantId = tenantId,
            PacienteId = pacienteId,
            UsuarioId = UsuarioIdActual(),
            Entidad = entidad,
            EntidadId = entidadId,
            Accion = accion,
            Campo = campo,
            ValorAnterior = anterior,
            ValorNuevo = nuevo
        });
    }
}
