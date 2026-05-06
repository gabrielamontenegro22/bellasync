namespace BellaSync.Domain.Common;

/// <summary>
/// Marca a una entidad como perteneciente a un Tenant (salón).
/// El ApplicationDbContext aplica un filtro global por TenantId
/// para garantizar aislamiento multi-tenant.
/// </summary>
public interface ITenantEntity
{
    Guid TenantId { get; set; }
}
