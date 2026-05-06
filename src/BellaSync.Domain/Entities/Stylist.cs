using BellaSync.Domain.Common;

namespace BellaSync.Domain.Entities;

/// <summary>
/// Estilista que presta servicios en el salón. Aislado por TenantId.
/// Por defecto es solo un recurso (la admin/recepcionista lo maneja).
/// Si en el futuro queremos que el estilista pueda iniciar sesión y
/// ver su propia agenda, asociamos UserId — nullable y opcional.
/// El borrado es lógico: IsActive=false. Las citas pasadas siguen
/// referenciándolo correctamente.
/// </summary>
public class Stylist : BaseEntity, ITenantEntity
{
    public Guid TenantId { get; set; }

    public string FullName { get; set; } = string.Empty;

    /// <summary>Teléfono de contacto. Opcional.</summary>
    public string? Phone { get; set; }

    /// <summary>
    /// Color hex (#RRGGBB) para identificar al estilista en la agenda visual.
    /// Si es null, la UI usa un color por defecto.
    /// </summary>
    public string? Color { get; set; }

    /// <summary>Fecha de ingreso al salón. Opcional.</summary>
    public DateOnly? HireDate { get; set; }

    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Si está asociado a un User del sistema (puede iniciar sesión y
    /// ver su agenda), guardamos el UserId aquí. Por defecto null en MVP.
    /// </summary>
    public Guid? UserId { get; set; }

    /// <summary>Servicios que el estilista sabe realizar (M:N).</summary>
    public ICollection<StylistService> StylistServices { get; set; } = new List<StylistService>();
}
