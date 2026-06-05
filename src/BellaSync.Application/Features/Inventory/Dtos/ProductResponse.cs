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

    /// <summary>FK a la categoría del tenant. Hidratada con el name+tone abajo.</summary>
    public Guid CategoryId { get; init; }

    /// <summary>Nombre legible de la categoría (ej. "Cabello", "Pestañas").</summary>
    public string CategoryName { get; init; } = string.Empty;

    public string Unit { get; init; } = string.Empty;
    public int Stock { get; init; }
    public int MinStock { get; init; }
    public decimal Cost { get; init; }

    /// <summary>"Rose" | "Amber" | "Sand" | "Olive" | "Wine" | "Mist". Heredado de la categoría.</summary>
    public string Tone { get; init; } = string.Empty;

    public DateTime? LastInAt { get; init; }
    public bool IsActive { get; init; }
    public DateTime CreatedAt { get; init; }

    /// <summary>"ok" | "warn" | "low" | "out". Espejo del helper statusOf() del mockup.</summary>
    public string Status { get; init; } = "ok";
}
