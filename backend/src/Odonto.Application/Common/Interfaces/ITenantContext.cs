namespace Odonto.Application.Common.Interfaces;

/// <summary>
/// Resuelve, para el request actual, a qué tenant pertenece el usuario autenticado.
/// El SuperAdmin no pertenece a ningún tenant y ve todo (EsSuperAdmin = true).
/// Se usa desde el DbContext para aplicar los global query filters por TenantId.
/// </summary>
public interface ITenantContext
{
    Guid? TenantId { get; }
    bool EsSuperAdmin { get; }
}
