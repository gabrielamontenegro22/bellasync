using BellaSync.Application.Common.Handlers;
using BellaSync.Application.Features.Services.Dtos;
using BellaSync.Domain.Entities;

namespace BellaSync.Application.Features.Services.UpdateService;

public sealed record UpdateServiceCommand(
    Guid Id,
    string Name,
    string? Description,
    ServiceCategory Category,
    int DurationMinutes,
    decimal Price,
    decimal CommissionPercentage,
    string? Color,
    bool RequiresDeposit,
    decimal DepositPercentage,
    bool IsActive) : ICommand<ServiceResponse>;
