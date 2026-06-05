using BellaSync.Application.Common.Handlers;
using BellaSync.Application.Features.Inventory.Dtos;

namespace BellaSync.Application.Features.Inventory.RegisterMovement;

/// <summary>
/// Registra un movimiento de inventario. Para Inflow/Outflow, Qty es el
/// delta (cuántas unidades suman/restan). Para Adjustment, Qty es el
/// NUEVO stock total (no el delta).
/// </summary>
public sealed record RegisterMovementCommand(
    Guid ProductId,
    /// <summary>"Inflow" | "Outflow" | "Adjustment"</summary>
    string Kind,
    int Qty,
    string Reason,
    string? Notes,
    Guid? RegisteredByUserId
) : ICommand<ProductMovementResponse>;
