using BellaSync.Domain.Common;

namespace BellaSync.Domain.Entities;

/// <summary>
/// Roles disponibles dentro de un salón.
/// SuperAdmin es a nivel SaaS (no pertenece a ningún Tenant);
/// el resto son roles dentro del salón.
/// </summary>
public enum UserRole
{
    SuperAdmin = 0,
    SalonAdmin = 1,
    Receptionist = 2,
    Stylist = 3
}

/// <summary>
/// Usuario del sistema. Cada usuario pertenece a un Tenant
/// (excepto los SuperAdmin del SaaS, que tendrán TenantId = Guid.Empty).
/// La autenticación se hace por email + contraseña hasheada con BCrypt.
/// </summary>
public class User : BaseEntity, ITenantEntity
{
    public Guid TenantId { get; set; }

    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;

    public UserRole Role { get; set; } = UserRole.SalonAdmin;
    public bool IsActive { get; set; } = true;

    public DateTime? LastLoginAt { get; set; }

    // Navegación inversa
    public Tenant? Tenant { get; set; }
}
