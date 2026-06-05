using BellaSync.Domain.Common;
using BellaSync.Domain.ValueObjects;

namespace BellaSync.Domain.Entities;

/// <summary>
/// Producto del inventario del salón. Insumo (tinte, decolorante, esmalte)
/// o accesorio (algodón, toallas, capas).
///
/// Diseño:
///   - El stock es un int (unidades enteras del producto en la unidad que
///     defina el salón — "frasco", "tubo", "kg", "500ml"). El usuario
///     elige la unidad libre, el sistema solo cuenta enteros.
///   - El stock se muta SOLO a través de los métodos verbales
///     `RegisterInflow`, `RegisterOutflow`, `AdjustTo`. Estos métodos
///     deben llamarse junto con la creación de un ProductMovement —
///     el handler asegura la atomicidad.
///   - LastInAt rastrea cuándo entró stock por última vez (para el
///     "hace N días" del mockup). Se actualiza SOLO en Inflow.
///   - Soft delete con IsActive — los productos archivados no aparecen
///     en el listado normal pero los movimientos históricos siguen
///     referenciándolos (no se rompe el historial).
/// </summary>
public class Product : BaseEntity, ITenantEntity
{
    private Product() { }

    /// <summary>
    /// Factory de creación. Stock arranca en 0 — para cargarle stock
    /// inicial, registrar un Inflow con motivo "Stock inicial" después.
    /// Esto deja el primer movimiento en el historial.
    /// </summary>
    public static Product Create(
        Guid tenantId,
        string name,
        string brand,
        ProductCategory category,
        string unit,
        int minStock,
        Money cost,
        ProductTone tone,
        DateTime utcNow)
    {
        if (tenantId == Guid.Empty)
            throw new DomainException("TenantId es obligatorio.");
        if (string.IsNullOrWhiteSpace(name))
            throw new DomainException("El nombre del producto es obligatorio.");
        if (string.IsNullOrWhiteSpace(brand))
            throw new DomainException("La marca del producto es obligatoria.");
        if (string.IsNullOrWhiteSpace(unit))
            throw new DomainException("La unidad de medida es obligatoria (ej. 'frasco', 'tubo', '500ml').");
        if (minStock < 0)
            throw new DomainException("El stock mínimo no puede ser negativo.");
        if (cost.Amount < 0m)
            throw new DomainException("El costo no puede ser negativo.");

        // CreatedAt y UpdatedAt los maneja BaseEntity / SaveChangesAsync (auto).
        _ = utcNow;
        return new Product
        {
            TenantId = tenantId,
            Name = name.Trim(),
            Brand = brand.Trim(),
            Category = category,
            Unit = unit.Trim(),
            Stock = 0,
            MinStock = minStock,
            Cost = cost,
            Tone = tone,
            LastInAt = null,
            IsActive = true,
        };
    }

    /// <summary>Plumbing multi-tenant.</summary>
    public Guid TenantId { get; set; }

    public string Name { get; private set; } = string.Empty;
    public string Brand { get; private set; } = string.Empty;
    public ProductCategory Category { get; private set; }

    /// <summary>
    /// Unidad de medida visible (texto libre): "frasco", "tubo", "kg",
    /// "500ml", "caja x100". No la usamos para conversión — solo display.
    /// </summary>
    public string Unit { get; private set; } = string.Empty;

    /// <summary>Stock actual en unidades enteras.</summary>
    public int Stock { get; private set; }

    /// <summary>
    /// Stock mínimo para alerta. Si stock &lt; minStock se considera "stock bajo".
    /// minStock = 0 desactiva la alerta para este producto.
    /// </summary>
    public int MinStock { get; private set; }

    /// <summary>Costo unitario (lo que pagó el salón al proveedor). Para valor del inventario.</summary>
    public Money Cost { get; private set; } = Money.Zero;

    public ProductTone Tone { get; private set; }

    /// <summary>
    /// Última vez que se registró una entrada (Inflow) para este producto.
    /// Sirve para mostrar "hace N días" en la tabla y detectar productos
    /// que no se reponen hace tiempo.
    /// </summary>
    public DateTime? LastInAt { get; private set; }

    /// <summary>
    /// Soft delete. Los archivados no aparecen en listas por default
    /// pero los ProductMovement viejos siguen referenciándolos.
    /// </summary>
    public bool IsActive { get; private set; } = true;

    // CreatedAt y UpdatedAt vienen de BaseEntity.

    // ===== MÉTODOS VERBALES =====

    /// <summary>
    /// Actualiza los campos editables del producto (nombre, marca, categoría,
    /// unidad, stock mínimo, costo, tono). El stock NO se cambia desde acá
    /// (para eso se usan los movimientos).
    /// </summary>
    public void UpdateDetails(
        string name,
        string brand,
        ProductCategory category,
        string unit,
        int minStock,
        Money cost,
        ProductTone tone,
        DateTime utcNow)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new DomainException("El nombre del producto es obligatorio.");
        if (string.IsNullOrWhiteSpace(brand))
            throw new DomainException("La marca del producto es obligatoria.");
        if (string.IsNullOrWhiteSpace(unit))
            throw new DomainException("La unidad de medida es obligatoria.");
        if (minStock < 0)
            throw new DomainException("El stock mínimo no puede ser negativo.");
        if (cost.Amount < 0m)
            throw new DomainException("El costo no puede ser negativo.");

        _ = utcNow;  // SaveChangesAsync setea UpdatedAt automáticamente.
        Name = name.Trim();
        Brand = brand.Trim();
        Category = category;
        Unit = unit.Trim();
        MinStock = minStock;
        Cost = cost;
        Tone = tone;
    }

    /// <summary>
    /// Registra una entrada de stock. Suma qty al stock y actualiza
    /// LastInAt. El caller (handler) tiene que crear el ProductMovement
    /// correspondiente en la misma transacción.
    /// </summary>
    public void RegisterInflow(int qty, DateTime utcNow)
    {
        if (qty <= 0) throw new DomainException("La cantidad debe ser mayor a cero.");
        Stock += qty;
        LastInAt = utcNow;
    }

    /// <summary>
    /// Registra una salida de stock. Resta qty del stock — si no alcanza,
    /// throw (no permitimos stock negativo, fuerza al usuario a hacer
    /// un ajuste explícito si el conteo físico no cuadra).
    /// </summary>
    public void RegisterOutflow(int qty, DateTime utcNow)
    {
        if (qty <= 0) throw new DomainException("La cantidad debe ser mayor a cero.");
        if (qty > Stock)
            throw new DomainException(
                $"No alcanza el stock de '{Name}'. Stock actual: {Stock}, cantidad pedida: {qty}. " +
                "Si el conteo físico no cuadra, registrá un ajuste en vez de una salida.");
        _ = utcNow;
        Stock -= qty;
    }

    /// <summary>
    /// Ajusta el stock a un valor absoluto. Usado para correcciones
    /// tras inventario físico, vencimientos en lote, etc. La auditoría
    /// queda en el ProductMovement (StockBefore + StockAfter).
    /// </summary>
    public void AdjustTo(int newStock, DateTime utcNow)
    {
        if (newStock < 0) throw new DomainException("El stock ajustado no puede ser negativo.");
        _ = utcNow;
        Stock = newStock;
    }

    /// <summary>Archivar. Idempotente.</summary>
    public void Archive(DateTime utcNow)
    {
        _ = utcNow;
        IsActive = false;
    }

    /// <summary>Reactivar. Idempotente.</summary>
    public void Reactivate(DateTime utcNow)
    {
        _ = utcNow;
        IsActive = true;
    }
}
