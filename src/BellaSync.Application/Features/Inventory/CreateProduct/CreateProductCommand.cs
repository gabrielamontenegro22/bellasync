using BellaSync.Application.Common.Handlers;
using BellaSync.Application.Features.Inventory.Dtos;

namespace BellaSync.Application.Features.Inventory.CreateProduct;

public sealed record CreateProductCommand(
    string Name,
    string Brand,
    /// <summary>"Hair" | "Nails" | "Hairremoval" | "Spa" | "Accessories"</summary>
    string Category,
    string Unit,
    int MinStock,
    decimal Cost,
    /// <summary>"Rose" | "Amber" | "Sand" | "Olive" | "Wine" | "Mist". Opcional → default por categoría.</summary>
    string? Tone
) : ICommand<ProductResponse>;
