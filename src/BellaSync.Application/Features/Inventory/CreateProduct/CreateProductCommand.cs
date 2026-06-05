using BellaSync.Application.Common.Handlers;
using BellaSync.Application.Features.Inventory.Dtos;

namespace BellaSync.Application.Features.Inventory.CreateProduct;

public sealed record CreateProductCommand(
    string Name,
    string Brand,
    /// <summary>Id de la categoría del tenant. La admin las gestiona en /inventario.</summary>
    Guid CategoryId,
    string Unit,
    int MinStock,
    decimal Cost
) : ICommand<ProductResponse>;
