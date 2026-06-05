namespace BellaSync.Application.Features.Inventory.Dtos;

/// <summary>
/// Movimiento del historial de un producto. El handler de "ver historial"
/// devuelve una lista de estos.
/// </summary>
public sealed class ProductMovementResponse
{
    public Guid Id { get; init; }
    public Guid ProductId { get; init; }

    /// <summary>"Inflow" | "Outflow" | "Adjustment"</summary>
    public string Kind { get; init; } = string.Empty;

    public int Qty { get; init; }
    public int StockBefore { get; init; }
    public int StockAfter { get; init; }
    public string Reason { get; init; } = string.Empty;
    public string? Notes { get; init; }

    public Guid? RegisteredByUserId { get; init; }
    public string? RegisteredByUserName { get; init; }

    public DateTime RegisteredAt { get; init; }
}
