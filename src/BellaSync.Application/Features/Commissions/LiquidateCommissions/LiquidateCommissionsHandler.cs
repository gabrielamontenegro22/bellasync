using BellaSync.Application.Common.Errors;
using BellaSync.Application.Common.Handlers;
using BellaSync.Application.Common.Interfaces;
using BellaSync.Application.Common.Results;
using BellaSync.Application.Features.Commissions.Dtos;
using BellaSync.Domain.Entities;
using BellaSync.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BellaSync.Application.Features.Commissions.LiquidateCommissions;

public sealed class LiquidateCommissionsHandler
    : ICommandHandler<LiquidateCommissionsCommand, CommissionPayoutResponse>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentTenantService _currentTenant;
    private readonly IClock _clock;
    private readonly ILogger<LiquidateCommissionsHandler> _logger;

    public LiquidateCommissionsHandler(
        IApplicationDbContext db,
        ICurrentTenantService currentTenant,
        IClock clock,
        ILogger<LiquidateCommissionsHandler> logger)
    {
        _db = db;
        _currentTenant = currentTenant;
        _clock = clock;
        _logger = logger;
    }

    public async Task<Result<CommissionPayoutResponse>> HandleAsync(
        LiquidateCommissionsCommand command, CancellationToken ct)
    {
        // 0. Validar rango antes de gastar IO. UI puede mandar invertido si
        //    el picker se calibra mal o el usuario juega con las fechas.
        if (command.PeriodFrom > command.PeriodTo)
            return ApplicationError.Validation(
                "commission.bad_range",
                "El período es inválido: 'desde' es posterior a 'hasta'.");

        // 1. Validar que el estilista exista y sea del tenant actual.
        var stylist = await _db.Stylists
            .FirstOrDefaultAsync(s => s.Id == command.StylistId, ct);
        if (stylist is null)
            return ApplicationError.NotFound("stylist.not_found", "Estilista no encontrado.");

        // 2. Money valida >= 0; factory de CommissionPayout valida > 0.
        Money amount;
        try
        {
            amount = Money.Create(command.Amount);
        }
        catch (BellaSync.Domain.Common.DomainException ex)
        {
            return ApplicationError.Validation("commission.invalid_amount", ex.Message);
        }

        CommissionPayout payout;
        try
        {
            payout = CommissionPayout.Create(
                tenantId: _currentTenant.TenantId,
                stylistId: stylist.Id,
                amount: amount,
                periodFrom: command.PeriodFrom,
                periodTo: command.PeriodTo,
                paidByUserId: command.PaidByUserId,
                notes: command.Notes,
                utcNow: _clock.UtcNow);
        }
        catch (BellaSync.Domain.Common.DomainException ex)
        {
            return ApplicationError.Validation("commission.invalid", ex.Message);
        }

        _db.CommissionPayouts.Add(payout);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Liquidación {PayoutId}: ${Amount} a estilista {StylistId} por período {From}–{To}",
            payout.Id, payout.Amount.Amount, payout.StylistId, payout.PeriodFrom, payout.PeriodTo);

        return Result<CommissionPayoutResponse>.Success(new CommissionPayoutResponse
        {
            Id = payout.Id,
            StylistId = payout.StylistId,
            StylistName = stylist.FullName,
            Amount = payout.Amount.Amount,
            PeriodFrom = payout.PeriodFrom.ToString("yyyy-MM-dd"),
            PeriodTo = payout.PeriodTo.ToString("yyyy-MM-dd"),
            PaidAt = payout.PaidAt,
            PaidByUserId = payout.PaidByUserId,
            Notes = payout.Notes,
        });
    }
}
