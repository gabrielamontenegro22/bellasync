using BellaSync.Domain.Common;

namespace BellaSync.Domain.Entities;

/// <summary>
/// Estilista que presta servicios en el salón. Aislado por TenantId.
/// Por defecto es solo un recurso (la admin/recepcionista lo maneja).
/// Si en el futuro queremos que el estilista pueda iniciar sesión y
/// ver su propia agenda, asociamos UserId — nullable y opcional.
/// El borrado es lógico: Status=Inactive. Las citas pasadas siguen
/// referenciándolo correctamente.
///
/// Setters privados: el estilista solo se muta vía métodos verbales
/// (`Rename`, `GoOnVacation`, `Reactivate`, `Archive`, etc.).
/// </summary>
public class Stylist : BaseEntity, ITenantEntity
{
    private Stylist() { }

    /// <summary>Factory: crea un estilista nuevo con invariantes validadas.</summary>
    public static Stylist Create(
        Guid tenantId,
        string fullName,
        string role,
        string? email = null,
        string? phone = null,
        string? idNumber = null,
        string? color = null,
        DateOnly? hireDate = null)
    {
        if (string.IsNullOrWhiteSpace(fullName))
            throw new DomainException("El nombre del estilista es obligatorio.");
        if (string.IsNullOrWhiteSpace(role))
            throw new DomainException("El cargo del estilista es obligatorio.");

        var stylist = new Stylist { TenantId = tenantId };
        stylist.FullName = fullName.Trim();
        stylist.Role = role.Trim();
        stylist.Email = NormalizeOptional(email)?.ToLowerInvariant();
        stylist.Phone = NormalizeOptional(phone);
        stylist.IdNumber = NormalizeOptional(idNumber);
        stylist.Color = NormalizeOptional(color);
        stylist.HireDate = hireDate;
        stylist.Status = StylistStatus.Active;
        stylist.UserId = null;
        return stylist;
    }

    // Plumbing multi-tenant (ver Service.cs para la justificación).
    public Guid TenantId { get; set; }

    public string FullName { get; private set; } = string.Empty;

    /// <summary>
    /// Cargo del estilista dentro del salón.
    /// String libre porque los salones pueden inventar roles propios
    /// ("Maquillador profesional", "Asistente de color", etc.).
    /// Sugeridos: Estilista, Estilista Senior, Colorista, Manicurista,
    /// Maquilladora, Esteticista, Aprendiz, Recepcionista.
    /// </summary>
    public string Role { get; private set; } = "Estilista";

    /// <summary>
    /// Email de contacto del estilista. NO confundir con el email del User
    /// asociado (que es para login). Este es solo para notificaciones internas
    /// del salón. Opcional.
    /// </summary>
    public string? Email { get; private set; }

    /// <summary>Teléfono de contacto. Opcional.</summary>
    public string? Phone { get; private set; }

    /// <summary>
    /// Cédula de ciudadanía. Se almacena tal cual la escribe la administradora
    /// (con o sin puntos). Sirve para liquidación y trazabilidad.
    /// </summary>
    public string? IdNumber { get; private set; }

    /// <summary>
    /// Color hex (#RRGGBB) para identificar al estilista en la agenda visual.
    /// Si es null, la UI usa un color por defecto.
    /// </summary>
    public string? Color { get; private set; }

    /// <summary>Fecha de ingreso al salón. Opcional.</summary>
    public DateOnly? HireDate { get; private set; }

    /// <summary>
    /// Estado del estilista. Active / Vacation / Inactive.
    /// </summary>
    public StylistStatus Status { get; private set; } = StylistStatus.Active;

    /// <summary>
    /// Si está asociado a un User del sistema (puede iniciar sesión y
    /// ver su agenda), guardamos el UserId aquí. Por defecto null en MVP.
    /// </summary>
    public Guid? UserId { get; private set; }

    /// <summary>Servicios que el estilista sabe realizar (M:N).</summary>
    public ICollection<StylistService> StylistServices { get; private set; } = new List<StylistService>();

    // ===== MÉTODOS VERBALES =====

    /// <summary>Cambia el nombre. Valida que no sea vacío.</summary>
    public void Rename(string newName)
    {
        if (string.IsNullOrWhiteSpace(newName))
            throw new DomainException("El nombre del estilista es obligatorio.");
        FullName = newName.Trim();
    }

    /// <summary>Cambia el cargo. Valida que no sea vacío.</summary>
    public void ChangeRole(string newRole)
    {
        if (string.IsNullOrWhiteSpace(newRole))
            throw new DomainException("El cargo del estilista es obligatorio.");
        Role = newRole.Trim();
    }

    /// <summary>Actualiza datos de contacto (todos opcionales).</summary>
    public void UpdateContact(string? email, string? phone, string? idNumber)
    {
        Email = NormalizeOptional(email)?.ToLowerInvariant();
        Phone = NormalizeOptional(phone);
        IdNumber = NormalizeOptional(idNumber);
    }

    /// <summary>Cambia el color visual en la agenda. Null para resetear.</summary>
    public void UpdateColor(string? color)
    {
        Color = NormalizeOptional(color);
    }

    /// <summary>Cambia fecha de ingreso (típicamente para corregir un dato).</summary>
    public void SetHireDate(DateOnly? hireDate)
    {
        HireDate = hireDate;
    }

    /// <summary>Asocia un User para que el estilista pueda hacer login.</summary>
    public void LinkToUser(Guid userId)
    {
        UserId = userId;
    }

    /// <summary>Desvincula del User asociado.</summary>
    public void UnlinkUser()
    {
        UserId = null;
    }

    /// <summary>
    /// Marca al estilista como en vacaciones (no toma citas).
    /// Aceptado desde cualquier estado anterior — la UX decide cuándo es legítimo
    /// poner a un estilista directamente en vacaciones desde Inactive.
    /// </summary>
    public void GoOnVacation() => Status = StylistStatus.Vacation;

    /// <summary>Vuelve a Active desde Vacation o Inactive.</summary>
    public void Reactivate() => Status = StylistStatus.Active;

    /// <summary>Soft delete: marca como Inactive. Idempotente.</summary>
    public void Archive() => Status = StylistStatus.Inactive;

    /// <summary>
    /// Asigna un servicio al estilista. Idempotente: si ya estaba asignado,
    /// no duplica. La entidad raíz controla su agregado.
    /// </summary>
    public void AssignService(Guid serviceId, DateTime utcNow)
    {
        if (StylistServices.Any(ss => ss.ServiceId == serviceId)) return;

        StylistServices.Add(new StylistService
        {
            StylistId = Id,
            ServiceId = serviceId,
            TenantId = TenantId,
            AssignedAt = utcNow,
        });
    }

    /// <summary>
    /// Quita la asignación de un servicio. Devuelve la entidad removida (o null)
    /// para que el caller pueda hacer `_db.StylistServices.Remove(...)` si trabaja
    /// con un DbContext con cambios trackeados.
    /// </summary>
    public StylistService? UnassignService(Guid serviceId)
    {
        var existing = StylistServices.FirstOrDefault(ss => ss.ServiceId == serviceId);
        if (existing is null) return null;
        StylistServices.Remove(existing);
        return existing;
    }

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
