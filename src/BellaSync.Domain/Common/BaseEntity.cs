namespace BellaSync.Domain.Common;

/// <summary>
/// Entidad base con identificador y timestamps de auditoría.
/// Todas las entidades persistibles del dominio heredan de aquí.
/// </summary>
public abstract class BaseEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
}
