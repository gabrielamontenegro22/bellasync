using BellaSync.Application.Common.Handlers;
using BellaSync.Application.Features.Inventory.Dtos;

namespace BellaSync.Application.Features.Inventory.UpdateProduct;

/// <summary>
/// Actualiza un producto. NewStock es opcional — si viene y es distinto
/// del actual, el handler crea automáticamente un ProductMovement tipo
/// Adjustment para mantener trazabilidad del cambio (auditoría).
///
/// Esto evita que la admin tenga que ir a "Registrar movimiento" → Ajuste
/// para algo común como "hice inventario físico y conté 20 en vez de 25".
/// Cambia el número en el form, guarda, y el historial registra el ajuste
/// automáticamente.
/// </summary>
public sealed record UpdateProductCommand(
    Guid Id,
    string Name,
    string Brand,
    Guid CategoryId,
    string Unit,
    int MinStock,
    decimal Cost,
    /// <summary>Si null, el stock no se toca. Si viene, se ajusta y se crea movimiento.</summary>
    int? NewStock
) : ICommand<ProductResponse>;
