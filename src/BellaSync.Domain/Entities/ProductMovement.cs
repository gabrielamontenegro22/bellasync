using BellaSync.Domain.Common;

namespace BellaSync.Domain.Entities;

/// <summary>
/// Movimiento de inventario. Cada entrada/salida/ajuste del stock de un
/// Product genera UNO de estos registros — son inmutables y forman el
/// historial completo del producto.
///
/// StockBefore + StockAfter son snapshots para que el historial sea
/// legible directamente sin tener que reconstruir el estado del stock
/// en el momento del movimiento. Si cambia la lógica de cómo calculamos
/// el stock, los movimientos viejos siguen mostrando el contexto real
/// que tenían cuando ocurrieron.
///
/// AppointmentId NO existe en el MVP — el usuario decidió mantener el
/// inventario simple (sin link a citas, sin auto-descuento por servicio).
/// Si en el futuro hace falta, se agrega como columna nullable.
/// </summary>
public class ProductMovement : BaseEntity, ITenantEntity
{
    private ProductMovement() { }

    public static ProductMovement Create(
        Guid tenantId,
        Guid productId,
        ProductMovementKind kind,
        int qty,
        string reason,
        int stockBefore,
        int stockAfter,
        string? notes,
        Guid? registeredByUserId,
        DateTime utcNow)
    {
        if (tenantId == Guid.Empty)
            throw new DomainException("TenantId es obligatorio.");
        if (productId == Guid.Empty)
            throw new DomainException("ProductId es obligatorio.");
        // Para Inflow/Outflow la cantidad debe ser > 0 (no se entran ni se
        // sacan 0 unidades). Para Adjustment se permite 0 — significa
        // "el stock total ahora es 0" (típicamente cuando se hace inventario
        // físico y no quedó nada del producto).
        if (kind == ProductMovementKind.Adjustment)
        {
            if (qty < 0)
                throw new DomainException("El stock ajustado no puede ser negativo.");
        }
        else
        {
            if (qty <= 0)
                throw new DomainException("La cantidad debe ser mayor a cero.");
        }
        if (string.IsNullOrWhiteSpace(reason))
            throw new DomainException("El motivo del movimiento es obligatorio.");
        if (stockBefore < 0 || stockAfter < 0)
            throw new DomainException("Los snapshots de stock no pueden ser negativos.");

        return new ProductMovement
        {
            TenantId = tenantId,
            ProductId = productId,
            Kind = kind,
            Qty = qty,
            Reason = reason.Trim(),
            StockBefore = stockBefore,
            StockAfter = stockAfter,
            Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim(),
            RegisteredByUserId = registeredByUserId,
            RegisteredAt = utcNow,
        };
    }

    public Guid TenantId { get; set; }

    public Guid ProductId { get; private set; }
    public Product? Product { get; private set; }

    public ProductMovementKind Kind { get; private set; }

    /// <summary>
    /// Cantidad del movimiento. Para Inflow/Outflow es la cantidad que
    /// se sumó/restó. Para Adjustment es el nuevo stock total
    /// (NO el delta) — para reconstruir el delta usar StockAfter-StockBefore.
    /// </summary>
    public int Qty { get; private set; }

    /// <summary>
    /// Motivo del movimiento. Texto pre-definido del frontend (ej. "Compra
    /// a proveedor", "Consumo en servicio", "Ajuste por inventario físico")
    /// pero acá lo guardamos como string libre para flexibilidad futura.
    /// </summary>
    public string Reason { get; private set; } = string.Empty;

    /// <summary>Stock del producto JUSTO ANTES del movimiento.</summary>
    public int StockBefore { get; private set; }

    /// <summary>Stock del producto JUSTO DESPUÉS del movimiento.</summary>
    public int StockAfter { get; private set; }

    /// <summary>Comentario opcional (libre) del usuario que registró el mov.</summary>
    public string? Notes { get; private set; }

    /// <summary>Usuario que registró el movimiento. null si fue automático.</summary>
    public Guid? RegisteredByUserId { get; private set; }

    public User? RegisteredByUser { get; private set; }

    /// <summary>Cuándo se registró (UTC).</summary>
    public DateTime RegisteredAt { get; private set; }
}
