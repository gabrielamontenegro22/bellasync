using BellaSync.Domain.Common;

namespace BellaSync.Domain.Entities;

/// <summary>
/// Tabla intermedia: qué servicios sabe hacer cada estilista.
/// Composite key (StylistId, ServiceId). Implementa ITenantEntity para
/// que el filtro global multi-tenant también lo aísle.
///
/// Si después necesitamos extras (comisión personalizada por estilista,
/// duración propia, etc.) agregamos campos aquí sin romper nada.
/// </summary>
public class StylistService : ITenantEntity
{
    public Guid TenantId { get; set; }

    public Guid StylistId { get; set; }
    public Stylist? Stylist { get; set; }

    public Guid ServiceId { get; set; }
    public Service? Service { get; set; }

    public DateTime AssignedAt { get; set; } = DateTime.UtcNow;
}
