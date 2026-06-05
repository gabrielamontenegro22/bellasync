using BellaSync.Application.Common.Handlers;
using BellaSync.Application.Features.Inventory.Dtos;

namespace BellaSync.Application.Features.Inventory.UpdateProduct;

/// <summary>
/// Actualiza los datos básicos de un producto (nombre, marca, categoría,
/// stock mínimo, costo). NO toca el stock — para cambiar stock está
/// "Registrar movimiento" (modal con tabs Entrada/Salida/Ajuste). Esta
/// separación deja el form de edición enfocado solo en metadata, y deja
/// los cambios de stock con auditoría completa vía ProductMovement.
/// </summary>
public sealed record UpdateProductCommand(
    Guid Id,
    string Name,
    string Brand,
    Guid CategoryId,
    int MinStock,
    decimal Cost
) : ICommand<ProductResponse>;
