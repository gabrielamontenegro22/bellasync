using BellaSync.Application.Common.Handlers;
using BellaSync.Application.Features.Inventory.Dtos;

namespace BellaSync.Application.Features.Inventory.ListMovements;

/// <summary>
/// Historial de movimientos de un producto puntual. Para el modal
/// "Ver historial" del menú del row. Ordenado desc por fecha.
/// </summary>
public sealed record ListMovementsQuery(Guid ProductId)
    : IQuery<IReadOnlyList<ProductMovementResponse>>;
