namespace BellaSync.Application.Features.Inventory.Dtos;

/// <summary>
/// KPIs del header de /inventario: las 4 cards + counts por estado para
/// los chips de filtro. Una sola llamada vs N counts separados.
/// </summary>
public sealed class InventorySummaryResponse
{
    /// <summary>Total de productos activos.</summary>
    public int TotalProducts { get; init; }

    /// <summary>
    /// Valor de inventario al costo: SUM(stock × cost) sobre productos activos.
    /// Es lo que vale el stock que tiene el salón ahora mismo.
    /// </summary>
    public decimal TotalValueCop { get; init; }

    /// <summary>Productos en estado "ok" o "warn" (con stock saludable o limítrofe).</summary>
    public int OkCount { get; init; }

    /// <summary>Productos con stock estrictamente menor al mínimo (no agotados).</summary>
    public int LowStockCount { get; init; }

    /// <summary>Productos con stock = 0.</summary>
    public int OutOfStockCount { get; init; }
}
