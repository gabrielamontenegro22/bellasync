using BellaSync.Application.Common.Errors;
using BellaSync.Application.Common.Handlers;
using BellaSync.Application.Common.Interfaces;
using BellaSync.Application.Common.Results;
using BellaSync.Application.Features.Cash.Dtos;
using BellaSync.Application.Features.Cash.Shared;
using BellaSync.Domain.Entities;
using BellaSync.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BellaSync.Application.Features.Cash.CreateCashClosing;

public sealed class CreateCashClosingHandler
    : ICommandHandler<CreateCashClosingCommand, CashClosingResponse>
{
    private static readonly TimeSpan ColombiaOffset = TimeSpan.FromHours(-5);

    private readonly IApplicationDbContext _db;
    private readonly ICurrentTenantService _currentTenant;
    private readonly IClock _clock;
    private readonly ILogger<CreateCashClosingHandler> _logger;

    public CreateCashClosingHandler(
        IApplicationDbContext db,
        ICurrentTenantService currentTenant,
        IClock clock,
        ILogger<CreateCashClosingHandler> logger)
    {
        _db = db;
        _currentTenant = currentTenant;
        _clock = clock;
        _logger = logger;
    }

    public async Task<Result<CashClosingResponse>> HandleAsync(
        CreateCashClosingCommand command, CancellationToken ct)
    {
        var todayCO = DateOnly.FromDateTime(_clock.UtcNow.Add(ColombiaOffset));

        DateOnly closedDate = todayCO;
        if (!string.IsNullOrWhiteSpace(command.ClosedDate))
        {
            if (!DateOnly.TryParseExact(command.ClosedDate, "yyyy-MM-dd", out closedDate))
                return ApplicationError.Validation("cash_closing.bad_date", "Formato de fecha inválido.");
        }

        // 1. Validar que no exista ya un cierre para esa fecha.
        var existing = await _db.CashClosings
            .AsNoTracking()
            .AnyAsync(cc => cc.ClosedDate == closedDate, ct);
        if (existing)
        {
            return ApplicationError.Conflict(
                "cash_closing.already_closed",
                $"La caja del día {closedDate:yyyy-MM-dd} ya fue cerrada.");
        }

        // 2. Snapshotear ventas y egresos en efectivo del día.
        //    Rango: [00:00, 24:00) en hora Colombia → UTC.
        var dayStartUtc = new DateTimeOffset(closedDate.ToDateTime(TimeOnly.MinValue), ColombiaOffset).UtcDateTime;
        var dayEndUtc = dayStartUtc.AddDays(1);

        var payments = await _db.Payments
            .AsNoTracking()
            .Where(p => p.RegisteredAt >= dayStartUtc && p.RegisteredAt < dayEndUtc)
            .ToListAsync(ct);
        var expenses = await _db.Expenses
            .AsNoTracking()
            .Where(e => e.RegisteredAt >= dayStartUtc && e.RegisteredAt < dayEndUtc)
            .ToListAsync(ct);

        var cashSales = payments
            .Where(p => p.Method == PaymentMethod.Cash)
            .Sum(p => p.Amount.Amount + p.Tip.Amount);
        var cashExpenses = expenses
            .Where(e => e.Method == PaymentMethod.Cash)
            .Sum(e => e.Amount.Amount);
        var totalAmount = payments.Sum(p => p.Amount.Amount + p.Tip.Amount);

        Money baseM, countedM, cashSalesM, cashExpensesM, totalM;
        try
        {
            baseM = Money.Create(command.BaseAmount);
            countedM = Money.Create(command.CountedCash);
            cashSalesM = Money.Create(cashSales);
            cashExpensesM = Money.Create(cashExpenses);
            totalM = Money.Create(totalAmount);
        }
        catch (BellaSync.Domain.Common.DomainException ex)
        {
            return ApplicationError.Validation("cash_closing.invalid_amount", ex.Message);
        }

        // 3. Factory del dominio — valida diff != 0 ⇒ nota obligatoria.
        CashClosing closing;
        try
        {
            closing = CashClosing.Create(
                tenantId: _currentTenant.TenantId,
                closedDate: closedDate,
                todayColombia: todayCO,
                baseAmount: baseM,
                cashSales: cashSalesM,
                cashExpenses: cashExpensesM,
                totalAmount: totalM,
                countedCash: countedM,
                diffNote: command.DiffNote,
                closedByUserId: command.ClosedByUserId,
                utcNow: _clock.UtcNow);
        }
        catch (BellaSync.Domain.Common.DomainException ex)
        {
            return ApplicationError.Validation("cash_closing.invalid", ex.Message);
        }

        _db.CashClosings.Add(closing);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Caja cerrada para {Date} — contado ${Counted}, esperado ${Expected}, diff ${Diff}",
            closedDate, closing.CountedCash.Amount, closing.ExpectedCash.Amount, closing.Diff);

        return Result<CashClosingResponse>.Success(CashClosingMapper.ToResponse(closing));
    }
}
