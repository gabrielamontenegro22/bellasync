using BellaSync.Application.Features.Inventory.Dtos;
using BellaSync.Domain.Entities;

namespace BellaSync.Application.Features.Inventory.Shared;

/// <summary>
/// Conversiones entidad → DTO. Calcula el status del stock para que el
/// frontend no tenga que duplicar la lógica.
/// </summary>
internal static class InventoryMapper
{
    /// <summary>
    /// Status del stock según el ratio stock/min. Espeja el helper
    /// statusOf() del mockup inventory.jsx:
    ///   stock == 0          → "out"
    ///   stock &lt; min         → "low"
    ///   stock &lt; min × 1.5   → "warn"
    ///   else                → "ok"
    ///
    /// minStock == 0 desactiva la alerta: cualquier stock &gt; 0 es "ok".
    /// </summary>
    public static string StatusOf(int stock, int minStock)
    {
        if (stock == 0) return "out";
        if (minStock <= 0) return "ok";
        var ratio = (double)stock / minStock;
        if (ratio < 1) return "low";
        if (ratio < 1.5) return "warn";
        return "ok";
    }

    public static ProductResponse ToResponse(Product p) => new()
    {
        Id = p.Id,
        Name = p.Name,
        Brand = p.Brand,
        CategoryId = p.CategoryId,
        CategoryName = p.Category?.Name ?? string.Empty,
        Unit = p.Unit,
        Stock = p.Stock,
        MinStock = p.MinStock,
        Cost = p.Cost.Amount,
        Tone = (p.Category?.Tone ?? ProductTone.Olive).ToString(),
        LastInAt = p.LastInAt,
        IsActive = p.IsActive,
        CreatedAt = p.CreatedAt,
        Status = StatusOf(p.Stock, p.MinStock),
    };

    public static ProductCategoryResponse ToResponse(ProductCategory c, int activeProductsCount) => new()
    {
        Id = c.Id,
        Name = c.Name,
        Tone = c.Tone.ToString(),
        IsActive = c.IsActive,
        ActiveProductsCount = activeProductsCount,
    };

    public static ProductMovementResponse ToResponse(ProductMovement m) => new()
    {
        Id = m.Id,
        ProductId = m.ProductId,
        Kind = m.Kind.ToString(),
        Qty = m.Qty,
        StockBefore = m.StockBefore,
        StockAfter = m.StockAfter,
        Reason = m.Reason,
        Notes = m.Notes,
        RegisteredByUserId = m.RegisteredByUserId,
        RegisteredByUserName = m.RegisteredByUser?.FullName,
        RegisteredAt = m.RegisteredAt,
    };
}
