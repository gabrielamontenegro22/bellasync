using BellaSync.Application.Common.Handlers;
using BellaSync.Application.Features.Inventory.Dtos;

namespace BellaSync.Application.Features.Inventory.ListCategories;

/// <summary>
/// Lista las categorías del salón. Por defecto solo activas; pasar
/// includeArchived=true para ver todas (gestión).
/// </summary>
public sealed record ListCategoriesQuery(bool IncludeArchived)
    : IQuery<IReadOnlyList<ProductCategoryResponse>>;
