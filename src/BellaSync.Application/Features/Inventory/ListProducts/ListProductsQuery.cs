using BellaSync.Application.Common.Handlers;
using BellaSync.Application.Features.Inventory.Dtos;

namespace BellaSync.Application.Features.Inventory.ListProducts;

/// <summary>
/// Lista los productos del tenant actual.
///   - category: si viene, filtra por esa categoría (Hair, Nails, etc.).
///   - status: si viene, filtra por estado calculado ("ok","low","out"). "all" = todos.
///   - query: substring case-insensitive contra name o brand.
///   - includeArchived: por default false (lista normal). Trueo para ver archivados.
/// </summary>
public sealed record ListProductsQuery(
    string? Category,
    string? Status,
    string? Query,
    bool IncludeArchived
) : IQuery<IReadOnlyList<ProductResponse>>;
