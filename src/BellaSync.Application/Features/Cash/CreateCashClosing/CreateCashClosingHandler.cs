using BellaSync.Application.Common;
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
    private readonly IApplicationDbContext _db;
    private readonly ICurrentTenantService _currentTenant;
    private readonly ICurrentUserService _currentUser;
    private readonly IReceptionPermissionsService _perms;
    private readonly IClock _clock;
    private readonly ILogger<CreateCashClosingHandler> _logger;

    public CreateCashClosingHandler(
        IApplicationDbContext db,
        ICurrentTenantService currentTenant,
        ICurrentUserService currentUser,
        IReceptionPermissionsService perms,
        IClock clock,
        ILogger<CreateCashClosingHandler> logger)
    {
        _db = db;
        _currentTenant = currentTenant;
        _currentUser = currentUser;
        _perms = perms;
        _clock = clock;
        _logger = logger;
    }

    public async Task<Result<CashClosingResponse>> HandleAsync(
        CreateCashClosingCommand command, CancellationToken ct)
    {
        // Guard de rol — configurable por tenant.
        // Admin: siempre puede firmar el cierre.
        // Recepción: solo si la admin activó CanCloseCash en
        // /configuracion/permisos. Snapshot del IReceptionPermissionsService
        // (cacheado scoped por request, mismo source de verdad que usan
        // los attributes [RequireReceptionPermission] en los controllers).
        if (!_currentUser.IsSalonAdmin)
        {
            var perms = await _perms.GetAsync(ct);
            if (!perms.CanCloseCash)
            {
                return ApplicationError.Forbidden(
                    "cash_closing.reception_not_allowed",
                    "El cierre de caja lo firma la administradora del salón. Pedile que pase a cerrarlo.");
            }
        }

        var todayCO = ColombiaTime.TodayFor(_clock.UtcNow);

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
        var (dayStartUtc, dayEndUtc) = ColombiaTime.DayRangeUtc(closedDate);

        var payments = await _db.Payments
            .AsNoTracking()
            .Where(p => p.RegisteredAt >= dayStartUtc && p.RegisteredAt < dayEndUtc)
            .ToListAsync(ct);
        var expenses = await _db.Expenses
            .AsNoTracking()
            .Where(e => e.RegisteredAt >= dayStartUtc && e.RegisteredAt < dayEndUtc)
            .ToListAsync(ct);

        // Anticipos validados del día (transferencias bancarias confirmadas).
        // Bug histórico C6 del audit: el snapshot ignoraba estos vouchers
        // y subreportaba ingresos del día (a veces millones para salones
        // con uso intenso de anticipos tipo balayage/keratina). La pantalla
        // /caja en vivo SÍ los incluía pero el cierre persistido NO →
        // reportes mensuales mostraban menos plata de la real.
        //
        // Bug 2026-06 (auditoría): EXCLUIR vouchers internos (aplicación
        // de crédito viejo). NO son plata nueva entrando — la pantalla
        // /caja en vivo los excluye también. Sin este filtro el snapshot
        // persistido diverge de lo que la admin vio al cerrar.
        var validatedVouchers = await _db.PaymentVouchers
            .AsNoTracking()
            .Where(v => v.Status == PaymentVoucherStatus.Validated
                     && v.DecidedAt != null
                     && v.DecidedAt >= dayStartUtc
                     && v.DecidedAt < dayEndUtc
                     && !v.IsInternalCredit)
            .Select(v => new { Amount = v.ReportedAmount.Amount })
            .ToListAsync(ct);

        var cashSales = payments
            .Where(p => p.Method == PaymentMethod.Cash)
            .Sum(p => p.Amount.Amount + p.Tip.Amount);
        var cashExpenses = expenses
            .Where(e => e.Method == PaymentMethod.Cash)
            .Sum(e => e.Amount.Amount);

        // totalAmount = payments del día + anticipos validados del día.
        // Los vouchers no van en cashSales (siempre son transferencia, no
        // efectivo), solo en el total general que se preserva en el snapshot.
        var totalAmount = payments.Sum(p => p.Amount.Amount + p.Tip.Amount)
                        + validatedVouchers.Sum(v => v.Amount);

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

        // Re-leer con Include para devolver el nombre del user que cerró
        // (lo necesita el historial inmediatamente después de crear).
        var created = await _db.CashClosings
            .AsNoTracking()
            .Include(cc => cc.ClosedByUser)
            .FirstAsync(cc => cc.Id == closing.Id, ct);

        return Result<CashClosingResponse>.Success(CashClosingMapper.ToResponse(created));
    }
}
