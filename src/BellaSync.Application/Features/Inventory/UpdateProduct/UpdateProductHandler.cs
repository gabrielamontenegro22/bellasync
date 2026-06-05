using BellaSync.Application.Common.Errors;
using BellaSync.Application.Common.Handlers;
using BellaSync.Application.Common.Interfaces;
using BellaSync.Application.Common.Results;
using BellaSync.Application.Features.Inventory.Dtos;
using BellaSync.Application.Features.Inventory.Shared;
using BellaSync.Domain.Common;
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

        // Validamos categoría destino: existe en el tenant y está activa.
        var category = await _db.ProductCategories
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == command.CategoryId, ct);

        if (category is null)
            return ApplicationError.Validation(
                "product.invalid_category",
                "La categoría seleccionada no existe en este salón.");

        if (!category.IsActive)
            return ApplicationError.Validation(
                "product.archived_category",
                "La categoría está archivada. Elegí otra activa.");

        Money cost;
        try { cost = Money.Create(command.Cost); }
        catch (DomainException ex)
        {
            return ApplicationError.Validation("product.invalid_cost", ex.Message);
        }

        // Actualiza metadata: nombre, marca, categoría, mínimo, costo.
        // El stock NO se cambia desde acá por diseño — usar el modal de
        // "Registrar movimiento" (tabs Entrada/Salida/Ajuste). Eso preserva
        // motivos descriptivos en el historial y mantiene una sola forma de
        // mutar stock.
        try
        {
            product.UpdateDetails(
                command.Name, command.Brand, command.CategoryId,
                command.MinStock, cost, _clock.UtcNow);
        }
        catch (DomainException ex)
        {
            return ApplicationError.Validation("product.invalid", ex.Message);
        }

        await _db.SaveChangesAsync(ct);

        // Re-leer con la nueva categoría adjunta.
        var fresh = await _db.Products
            .AsNoTracking()
            .Include(p => p.Category)
            .FirstAsync(p => p.Id == product.Id, ct);

        return Result<ProductResponse>.Success(InventoryMapper.ToResponse(fresh));
    }
}
