using BellaSync.Application.Common.Handlers;
using BellaSync.Application.Features.Services.Dtos;
using BellaSync.Domain.Entities;

namespace BellaSync.Application.Features.Services.CreateService;

/// <summary>
/// Comando para crear un servicio nuevo en el catálogo del salón actual.
/// El TenantId se obtiene del JWT en el handler (no viaja en el command).
/// </summary>
public sealed record CreateServiceCommand(
    string Name,
    string? Description,
    ServiceCategory Category,
    int DurationMinutes,
    decimal Price,
    decimal CommissionPercentage,
    string? Color,
    bool RequiresDeposit,
    decimal DepositPercentage) : ICommand<ServiceResponse>;
