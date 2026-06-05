using BellaSync.Domain.Common;

namespace BellaSync.Domain.Entities;

/// <summary>
/// Categoría de inventario del salón. Antes era un enum hardcoded
/// (Hair / Nails / Hairremoval / Spa / Accessories) pero pasamos a
/// entidad porque cada salón es distinto: una barbería solo usa
/// Cabello, una manicurista solo Uñas, un spa específico quizá tiene
/// "Pestañas" y "Cejas" — categorías que el enum no contemplaba.
///
/// La admin las gestiona desde /inventario → "Gestionar categorías":
///  - Crea cuantas necesite, con el nombre y el color visual que quiera.
///  - Edita el nombre o el color.
///  - Archiva las que dejen de usarse (siempre y cuando no tengan
///    productos activos asignados — eso lo valida el handler).
///
/// Cada tenant arranca con 5 categorías por defecto (sembradas en
/// onboarding y backfilleadas por migración para tenants existentes).
/// La admin puede borrar/renombrar las que no le sirvan.
/// </summary>
public class ProductCategory : BaseEntity, ITenantEntity
{
    private ProductCategory() { }

    public static ProductCategory Create(
        Guid tenantId,
        string name,
        ProductTone tone)
    {
        if (tenantId == Guid.Empty)
            throw new DomainException("TenantId es obligatorio.");
        if (string.IsNullOrWhiteSpace(name))
            throw new DomainException("El nombre de la categoría es obligatorio.");
        if (name.Trim().Length > 60)
            throw new DomainException("El nombre no puede pasar de 60 caracteres.");

        return new ProductCategory
        {
            TenantId = tenantId,
            Name = name.Trim(),
            Tone = tone,
            IsActive = true,
        };
    }

    public Guid TenantId { get; set; }
    public string Name { get; private set; } = string.Empty;
    public ProductTone Tone { get; private set; }
    public bool IsActive { get; private set; } = true;

    // ===== MÉTODOS VERBALES =====

    public void Rename(string newName)
    {
        if (string.IsNullOrWhiteSpace(newName))
            throw new DomainException("El nombre de la categoría es obligatorio.");
        if (newName.Trim().Length > 60)
            throw new DomainException("El nombre no puede pasar de 60 caracteres.");
        Name = newName.Trim();
    }

    public void ChangeTone(ProductTone tone) => Tone = tone;

    /// <summary>
    /// Archiva la categoría. NO se chequea acá si tiene productos
    /// activos — esa validación cross-aggregate es responsabilidad
    /// del handler (necesita el repositorio de productos).
    /// </summary>
    public void Archive() => IsActive = false;

    public void Reactivate() => IsActive = true;
}

/// <summary>
/// Color visual de la categoría (avatar del producto en la tabla y
/// chip de filtro). Vive como enum porque es una decisión cerrada de
/// look — agregar uno nuevo es una decisión de diseño, no un dato
/// del cliente.
/// </summary>
public enum ProductTone
{
    Rose = 0,
    Amber = 1,
    Sand = 2,
    Olive = 3,
    Wine = 4,
    Mist = 5,
}
