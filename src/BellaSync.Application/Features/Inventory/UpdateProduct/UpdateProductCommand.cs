using BellaSync.Application.Common.Handlers;
using BellaSync.Application.Features.Inventory.Dtos;

namespace BellaSync.Application.Features.Inventory.UpdateProduct;

public sealed record UpdateProductCommand(
    Guid Id,
    string Name,
    string Brand,
    Guid CategoryId,
    string Unit,
    int MinStock,
    decimal Cost
) : ICommand<ProductResponse>;
