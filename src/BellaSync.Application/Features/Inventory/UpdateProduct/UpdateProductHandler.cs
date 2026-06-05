using BellaSync.Application.Common.Errors;
using BellaSync.Application.Common.Handlers;
using BellaSync.Application.Common.Interfaces;
using BellaSync.Application.Common.Results;
using BellaSync.Application.Features.Inventory.Dtos;
using BellaSync.Application.Features.Inventory.Shared;
using BellaSync.Domain.Common;
using BellaSync.Domain.Entities;
using BellaSync.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace BellaSync.Application.Features.Inventory.UpdateProduct;

public sealed class UpdateProductHandler
    : ICommandHandler<UpdateProductCommand, ProductResponse>
{
    private readonly IApplicationDbContext _db;
    private readonly IClock _clock;

    public UpdateProductHandler(IApplicationDbContext db, IClock clock)
    {
        _db = db;
        _clock = clock;
    }

    public async Task<Result<ProductResponse>> HandleAsync(
        UpdateProductCommand command, CancellationToken ct)
    {
        var product = await _db.Products
            .FirstOrDefaultAsync(p => p.Id == command.Id, ct);

        if (product is null)
            return ApplicationError.NotFound("product.not_found", "Producto no encontrado.");

        if (!Enum.TryParse<ProductCategory>(command.Category, ignoreCase: true, out var cat))
            return ApplicationError.Validation("product.invalid_category", "Categoría inválida.");

        ProductTone tone = ProductTone.Olive;
        if (!string.IsNullOrWhiteSpace(command.Tone))
        {
            if (!Enum.TryParse<ProductTone>(command.Tone, ignoreCase: true, out tone))
                return ApplicationError.Validation("product.invalid_tone", "Tono inválido.");
        }
        else
        {
            tone = product.Tone;  // mantener el existente si no mandan
        }

        Money cost;
        try { cost = Money.Create(command.Cost); }
        catch (DomainException ex)
        {
            return ApplicationError.Validation("product.invalid_cost", ex.Message);
        }

        try
        {
            product.UpdateDetails(
                command.Name, command.Brand, cat, command.Unit,
                command.MinStock, cost, tone, _clock.UtcNow);
        }
        catch (DomainException ex)
        {
            return ApplicationError.Validation("product.invalid", ex.Message);
        }

        await _db.SaveChangesAsync(ct);

        return Result<ProductResponse>.Success(InventoryMapper.ToResponse(product));
    }
}
