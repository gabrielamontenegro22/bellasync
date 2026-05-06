namespace BellaSync.Application.Common.Interfaces;

/// <summary>
/// Resuelve el TenantId del request en curso.
/// Se obtiene del claim 'tenant_id' del JWT y lo consume el
/// ApplicationDbContext para aplicar el filtro global multi-tenant.
/// </summary>
public interface ICurrentTenantService
{
    /// <summary>
    /// Id del tenant del usuario autenticado, o Guid.Empty si no hay tenant
    /// (anónimo o SuperAdmin del SaaS).
    /// </summary>
    Guid TenantId { get; }

    /// <summary>
    /// Indica si el request actual está dentro del scope de un tenant
    /// (es decir, hay un usuario autenticado con tenant_id en el JWT).
    /// </summary>
    bool HasTenant { get; }
}
