using BellaSync.Application.Common;
using BellaSync.Application.Common.Errors;
using BellaSync.Application.Common.Handlers;
using BellaSync.Application.Common.Interfaces;
using BellaSync.Application.Common.Results;
using BellaSync.Application.Features.Stylists.TimeOff.Dtos;
using BellaSync.Domain.Common;
using BellaSync.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BellaSync.Application.Features.Stylists.TimeOff.AddStylistTimeOff;

public sealed class AddStylistTimeOffHandler
    : ICommandHandler<AddStylistTimeOffCommand, StylistTimeOffResponse>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentTenantService _currentTenant;
    private readonly IClock _clock;
    private readonly ILogger<AddStylistTimeOffHandler> _logger;

    public AddStylistTimeOffHandler(
        IApplicationDbContext db,
        ICurrentTenantService currentTenant,
        IClock clock,
        ILogger<AddStylistTimeOffHandler> logger)
    {
        _db = db;
        _currentTenant = currentTenant;
        _clock = clock;
        _logger = logger;
    }

    public async Task<Result<StylistTimeOffResponse>> HandleAsync(
        AddStylistTimeOffCommand command, CancellationToken ct)
    {
        if (!_currentTenant.HasTenant)
            return ApplicationError.Unauthorized("stylist.no_tenant", "Sesión inválida.");

        // Verificar que el estilista exista (en este tenant, el filter
        // global ya filtra).
        var stylistExists = await _db.Stylists
            .AnyAsync(s => s.Id == command.StylistId, ct);
        if (!stylistExists)
            return ApplicationError.NotFound("stylist.not_found", "Estilista no encontrado.");

        var today = ColombiaTime.TodayFor(_clock.UtcNow);
        StylistTimeOff timeOff;
        try
        {
            timeOff = StylistTimeOff.Create(
                tenantId: _currentTenant.TenantId,
                stylistId: command.StylistId,
                fromDate: command.FromDate,
                toDate: command.ToDate,
                todayColombia: today,
                reason: command.Reason);
        }
        catch (DomainException ex)
        {
            return ApplicationError.Validation("stylist.time_off_invalid", ex.Message);
        }

        _db.StylistTimeOffs.Add(timeOff);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "TimeOff agregado para estilista {StylistId}: {From} a {To} ({Reason})",
            command.StylistId, command.FromDate, command.ToDate, command.Reason ?? "sin razón");

        return Result<StylistTimeOffResponse>.Success(new StylistTimeOffResponse
        {
            Id = timeOff.Id,
            StylistId = timeOff.StylistId,
            FromDate = timeOff.FromDate,
            ToDate = timeOff.ToDate,
            Reason = timeOff.Reason,
            IsPast = timeOff.ToDate < today,
            CreatedAt = timeOff.CreatedAt,
        });
    }
}
