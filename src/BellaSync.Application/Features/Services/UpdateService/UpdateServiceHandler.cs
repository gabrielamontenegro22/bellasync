using BellaSync.Application.Common.Errors;
using BellaSync.Application.Common.Handlers;
using BellaSync.Application.Common.Interfaces;
using BellaSync.Application.Common.Results;
using BellaSync.Application.Features.Services.Dtos;
using BellaSync.Application.Features.Services.Shared;
using BellaSync.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BellaSync.Application.Features.Services.UpdateService;

public sealed class UpdateServiceHandler : ICommandHandler<UpdateServiceCommand, ServiceResponse>
{
    private readonly IApplicationDbContext _db;
    private readonly ILogger<UpdateServiceHandler> _logger;

    public UpdateServiceHandler(IApplicationDbContext db, ILogger<UpdateServiceHandler> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<Result<ServiceResponse>> HandleAsync(
        UpdateServiceCommand command, CancellationToken ct)
    {
        var service = await _db.Services.FirstOrDefaultAsync(s => s.Id == command.Id, ct);
        if (service is null)
        {
            return ApplicationError.NotFound(
                "service.not_found",
                $"No existe un servicio con id {command.Id}.");
        }

        var newName = command.Name.Trim();
        var nameChanged = !string.Equals(service.Name, newName, StringComparison.OrdinalIgnoreCase);

        // Si va a quedar activo y el nombre cambió, validar duplicado.
        if (command.IsActive && nameChanged)
        {
            var nameTaken = await _db.Services
                .AnyAsync(s => s.Id != command.Id && s.IsActive && s.Name == newName, ct);
            if (nameTaken)
            {
                return ApplicationError.Conflict(
                    "service.name_taken",
                    $"Ya existe otro servicio activo con el nombre \"{newName}\".");
            }
        }

        // Mutación vía métodos verbales del dominio.
        service.Rename(newName);
        service.UpdateDescription(command.Description);
        service.Recategorize(command.Category);
        service.UpdateDuration(command.DurationMinutes);
        service.UpdatePricing(
            Money.Create(command.Price),
            Percentage.Create(command.CommissionPercentage));
        service.UpdateColor(command.Color);

        if (command.RequiresDeposit)
            service.EnableDeposit(Percentage.Create(command.DepositPercentage));
        else
            service.DisableDeposit();

        if (command.IsActive) service.Reactivate();
        else service.Archive();

        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Servicio {ServiceId} actualizado en tenant {TenantId}",
            service.Id, service.TenantId);

        return Result<ServiceResponse>.Success(ServiceMapper.ToResponse(service));
    }
}
