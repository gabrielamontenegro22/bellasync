using BellaSync.Application.Common.Handlers;
using BellaSync.Application.Features.Inventory.Dtos;

namespace BellaSync.Application.Features.Inventory.ListProducts;

/// <summary>
/// Lista los productos del tenant actual.
///   - categoryId: si viene (no Guid.Empty), filtra por esa categoría.
///                 Las categorías ahora son por tenant (no enum hardcoded).
///   - status: si viene, filtra por estado calculado ("ok","low","out"). "all" = todos.
///   - query: substring case-insensitive contra name o brand.
///   - includeArchived: por default false (lista normal). True para ver archivados.
/// </summary>
public sealed record ListProductsQuery(
    Guid? CategoryId,
    string? Status,
    string? Query,
    bool IncludeArchived
) : IQuery<IReadOnlyList<ProductResponse>>;
