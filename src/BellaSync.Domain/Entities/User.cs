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
///
/// Setters privados: el user solo se muta vía métodos verbales
/// (`ChangePassword`, `MarkLogin`, `Archive`, `Reactivate`, `ChangeRole`).
/// </summary>
public class User : BaseEntity, ITenantEntity
{
    private User() { }

    /// <summary>
    /// Factory: crea un user nuevo. Recibe el password ya HASHEADO
    /// (la entidad NO conoce el algoritmo de hash — ese es Application).
    /// </summary>
    public static User Create(
        Guid tenantId,
        string email,
        string passwordHash,
        string fullName,
        UserRole role)
    {
        if (string.IsNullOrWhiteSpace(email))
            throw new DomainException("El email es obligatorio.");
        if (string.IsNullOrWhiteSpace(passwordHash))
            throw new DomainException("El password hash es obligatorio.");
        if (string.IsNullOrWhiteSpace(fullName))
            throw new DomainException("El nombre completo es obligatorio.");

        var user = new User();
        user.TenantId = tenantId;
        user.Email = email.Trim().ToLowerInvariant();
        user.PasswordHash = passwordHash;
        user.FullName = fullName.Trim();
        user.Role = role;
        user.IsActive = true;
        return user;
    }

    // TenantId es plumbing multi-tenant (set público requerido por
    // ITenantEntity y por el auto-set de SaveChangesAsync).
    public Guid TenantId { get; set; }

    public string Email { get; private set; } = string.Empty;
    public string PasswordHash { get; private set; } = string.Empty;
    public string FullName { get; private set; } = string.Empty;

    public UserRole Role { get; private set; } = UserRole.SalonAdmin;
    public bool IsActive { get; private set; } = true;

    public DateTime? LastLoginAt { get; private set; }

    // Navegación inversa
    public Tenant? Tenant { get; private set; }

    // ===== MÉTODOS VERBALES =====

    /// <summary>
    /// Cambia el password hash. La entidad NO conoce el algoritmo —
    /// el caller debe haber generado el hash con IPasswordHasher antes.
    /// </summary>
    public void ChangePassword(string newPasswordHash)
    {
        if (string.IsNullOrWhiteSpace(newPasswordHash))
            throw new DomainException("El password hash es obligatorio.");
        PasswordHash = newPasswordHash;
    }

    /// <summary>Marca el momento del último login exitoso.</summary>
    public void MarkLogin(DateTime utcNow) => LastLoginAt = utcNow;

    /// <summary>Cambia el rol del user (solo SalonAdmin puede hacerlo desde la API).</summary>
    public void ChangeRole(UserRole newRole) => Role = newRole;

    /// <summary>Cambia el nombre completo.</summary>
    public void Rename(string newName)
    {
        if (string.IsNullOrWhiteSpace(newName))
            throw new DomainException("El nombre completo es obligatorio.");
        FullName = newName.Trim();
    }

    /// <summary>Soft delete. Idempotente.</summary>
    public void Archive() => IsActive = false;

    /// <summary>Reactivar un user archivado. Idempotente.</summary>
    public void Reactivate() => IsActive = true;
}
