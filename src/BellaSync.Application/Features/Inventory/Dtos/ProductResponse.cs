namespace BellaSync.Application.Features.Inventory.Dtos;

/// <summary>
/// Producto del inventario tal como lo consume el frontend de /inventario.
///
/// Status se calcula server-side a partir de stock/minStock para no
/// duplicar la lógica en el cliente:
///   - "out"  → stock == 0
///   - "low"  → 0 &lt; stock &lt; minStock
///   - "warn" → minStock ≤ stock &lt; 1.5 × minStock (límite)
///   - "ok"   → stock ≥ 1.5 × minStock
/// </summary>
public sealed class ProductResponse
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Brand { get; init; } = string.Empty;

    /// <summary>"Hair" | "Nails" | "Hairremoval" | "Spa" | "Accessories"</summary>
    public string Category { get; init; } = string.Empty;

    public string Unit { get; init; } = string.Empty;
    public int Stock { get; init; }
    public int MinStock { get; init; }
    public decimal Cost { get; init; }

    /// <summary>"Rose" | "Amber" | "Sand" | "Olive" | "Wine" | "Mist"</summary>
    public string Tone { get; init; } = string.Empty;

    public DateTime? LastInAt { get; init; }
    public bool IsActive { get; init; }
    public DateTime CreatedAt { get; init; }

    /// <summary>"ok" | "warn" | "low" | "out". Espejo del helper statusOf() del mockup.</summary>
    public string Status { get; init; } = "ok";
}
