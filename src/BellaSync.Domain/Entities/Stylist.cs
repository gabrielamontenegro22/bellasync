using BellaSync.Domain.Common;

namespace BellaSync.Domain.Entities;

/// <summary>
/// Estilista que presta servicios en el salón. Aislado por TenantId.
/// Por defecto es solo un recurso (la admin/recepcionista lo maneja).
/// Si en el futuro queremos que el estilista pueda iniciar sesión y
/// ver su propia agenda, asociamos UserId — nullable y opcional.
/// El borrado es lógico: Status=Inactive. Las citas pasadas siguen
/// referenciándolo correctamente.
/// </summary>
public class Stylist : BaseEntity, ITenantEntity
{
    public Guid TenantId { get; set; }

    public string FullName { get; set; } = string.Empty;

    /// <summary>
    /// Cargo del estilista dentro del salón.
    /// String libre porque los salones pueden inventar roles propios
    /// ("Maquillador profesional", "Asistente de color", etc.).
    /// Sugeridos: Estilista, Estilista Senior, Colorista, Manicurista,
    /// Maquilladora, Esteticista, Aprendiz, Recepcionista.
    /// </summary>
    public string Role { get; set; } = "Estilista";

    /// <summary>
    /// Email de contacto del estilista. NO confundir con el email del User
    /// asociado (que es para login). Este es solo para notificaciones internas
    /// del salón. Opcional.
    /// </summary>
    public string? Email { get; set; }

    /// <summary>Teléfono de contacto. Opcional.</summary>
    public string? Phone { get; set; }

    /// <summary>
    /// Cédula de ciudadanía. Se almacena tal cual la escribe la administradora
    /// (con o sin puntos). Sirve para liquidación y trazabilidad.
    /// </summary>
    public string? IdNumber { get; set; }

    /// <summary>
    /// Color hex (#RRGGBB) para identificar al estilista en la agenda visual.
    /// Si es null, la UI usa un color por defecto.
    /// </summary>
    public string? Color { get; set; }

    /// <summary>Fecha de ingreso al salón. Opcional.</summary>
    public DateOnly? HireDate { get; set; }

    /// <summary>
    /// Estado del estilista. Reemplaza al antiguo IsActive (bool) y soporta
    /// además el caso "Vacation" donde sigue en el equipo pero no toma citas.
    /// </summary>
    public StylistStatus Status { get; set; } = StylistStatus.Active;

    /// <summary>
    /// Si está asociado a un User del sistema (puede iniciar sesión y
    /// ver su agenda), guardamos el UserId aquí. Por defecto null en MVP.
    /// </summary>
    public Guid? UserId { get; set; }

    /// <summary>Servicios que el estilista sabe realizar (M:N).</summary>
    public ICollection<StylistService> StylistServices { get; set; } = new List<StylistService>();
}
