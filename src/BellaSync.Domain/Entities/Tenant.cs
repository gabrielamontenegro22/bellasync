using BellaSync.Domain.Common;

namespace BellaSync.Domain.Entities;

/// <summary>
/// Representa un salón de belleza dentro del SaaS.
/// El Tenant es el "dueño" lógico de los datos: usuarios, citas,
/// inventario, clientes, pagos, etc. siempre pertenecen a un Tenant.
///
/// Setters privados: la entidad solo se muta vía métodos verbales
/// (`Rename`, `Deactivate`, `Reactivate`).
/// </summary>
public class Tenant : BaseEntity
{
    private Tenant() { }

    /// <summary>
    /// Factory: crea un tenant nuevo con invariantes validadas.
    /// El slug se valida pero NO se genera acá — la responsabilidad de
    /// generar slugs únicos pertenece al caller (que necesita acceso a
    /// la BD para verificar colisiones).
    /// </summary>
    public static Tenant Create(string name, string slug)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new DomainException("El nombre del salón es obligatorio.");
        if (string.IsNullOrWhiteSpace(slug))
            throw new DomainException("El slug del salón es obligatorio.");

        var tenant = new Tenant();
        tenant.Name = name.Trim();
        tenant.Slug = slug;
        tenant.IsActive = true;
        return tenant;
    }

    public string Name { get; private set; } = string.Empty;

    /// <summary>
    /// Identificador único legible para URLs (ej. "bella-spa-neiva").
    /// Se genera fuera de la entidad (típicamente con SlugGenerator) porque
    /// requiere chequeo de unicidad contra la BD.
    /// </summary>
    public string Slug { get; private set; } = string.Empty;

    public bool IsActive { get; private set; } = true;

    // Relación inversa: usuarios que pertenecen a este salón
    public ICollection<User> Users { get; private set; } = new List<User>();

    // ===== MÉTODOS VERBALES =====

    public void Rename(string newName)
    {
        if (string.IsNullOrWhiteSpace(newName))
            throw new DomainException("El nombre del salón es obligatorio.");
        Name = newName.Trim();
    }

    /// <summary>
    /// Desactivar el salón. Los users del salón no podrán iniciar sesión
    /// (Login chequea Tenant.IsActive). Idempotente.
    /// </summary>
    public void Deactivate() => IsActive = false;

    /// <summary>Reactivar el salón. Idempotente.</summary>
    public void Reactivate() => IsActive = true;
}
