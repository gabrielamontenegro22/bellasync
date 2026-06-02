using BellaSync.Application.Common.Errors;
using BellaSync.Application.Common.Handlers;
using BellaSync.Application.Common.Interfaces;
using BellaSync.Application.Common.Results;
using BellaSync.Application.Features.Services.Dtos;
using BellaSync.Application.Features.Services.Shared;
using BellaSync.Domain.Entities;
using BellaSync.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BellaSync.Application.Features.Services.CreateService;

/// <summary>
/// Handler: crea un Service nuevo. Pre-chequea duplicado de nombre activo
/// dentro del tenant (race condition residual queda atrapada por el
/// UniqueViolationExceptionHandler de WebApi → 409).
/// </summary>
public sealed class CreateServiceHandler : ICommandHandler<CreateServiceCommand, ServiceResponse>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentTenantService _currentTenant;
    private readonly ILogger<CreateServiceHandler> _logger;

    public CreateServiceHandler(
        IApplicationDbContext db,
        ICurrentTenantService currentTenant,
        ILogger<CreateServiceHandler> logger)
    {
        _db = db;
        _currentTenant = currentTenant;
        _logger = logger;
    }

    public async Task<Result<ServiceResponse>> HandleAsync(
        CreateServiceCommand command, CancellationToken ct)
    {
        var name = command.Name.Trim();

        var nameTaken = await _db.Services
            .AnyAsync(s => s.IsActive && s.Name == name, ct);
        if (nameTaken)
        {
            return ApplicationError.Conflict(
                "service.name_taken",
                $"Ya existe un servicio activo con el nombre \"{name}\".");
        }

        var deposit = command.RequiresDeposit
            ? Percentage.Create(command.DepositPercentage)
            : Percentage.Zero;

        var service = Service.Create(
            tenantId: _currentTenant.TenantId,
            name: name,
            category: command.Category,
            durationMinutes: command.DurationMinutes,
            price: Money.Create(command.Price),
            commission: Percentage.Create(command.CommissionPercentage),
            description: command.Description,
            color: command.Color,
            requiresDeposit: command.RequiresDeposit,
            depositPercentage: deposit);

        _db.Services.Add(service);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Servicio {ServiceName} ({ServiceId}) creado en tenant {TenantId}",
            service.Name, service.Id, service.TenantId);

        return Result<ServiceResponse>.Success(ServiceMapper.ToResponse(service));
    }
}
