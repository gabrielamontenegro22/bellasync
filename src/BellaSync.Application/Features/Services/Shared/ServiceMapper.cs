using BellaSync.Application.Features.Services.Dtos;
using BellaSync.Domain.Entities;

namespace BellaSync.Application.Features.Services.Shared;

/// <summary>
/// Mapeo Entity → DTO compartido entre handlers de Services.
/// Saca la repetición que aparecería en cada handler.
/// </summary>
internal static class ServiceMapper
{
    public static ServiceResponse ToResponse(Service s) => new()
    {
        Id = s.Id,
        Name = s.Name,
        Description = s.Description,
        Category = s.Category.ToString(),
        DurationMinutes = s.DurationMinutes,
        // Desempaquetar los VOs a primitivos para el contrato JSON.
        Price = s.Price.Amount,
        CommissionPercentage = s.CommissionPercentage.Value,
        Color = s.Color,
        IsActive = s.IsActive,
        RequiresDeposit = s.RequiresDeposit,
        DepositPercentage = s.DepositPercentage.Value,
        CreatedAt = s.CreatedAt,
        UpdatedAt = s.UpdatedAt,
    };
}
