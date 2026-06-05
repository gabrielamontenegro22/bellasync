using BellaSync.Application.Common.Handlers;
using BellaSync.Application.Features.Inventory.Dtos;

namespace BellaSync.Application.Features.Inventory.CreateCategory;

public sealed record CreateCategoryCommand(
    string Name,
    /// <summary>"Rose" | "Amber" | "Sand" | "Olive" | "Wine" | "Mist"</summary>
    string Tone
) : ICommand<ProductCategoryResponse>;
