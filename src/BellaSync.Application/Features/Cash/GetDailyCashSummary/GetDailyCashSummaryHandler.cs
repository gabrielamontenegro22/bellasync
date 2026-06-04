using BellaSync.Application.Common.Handlers;
using BellaSync.Application.Common.Interfaces;
using BellaSync.Application.Common.Results;
using BellaSync.Application.Features.Cash.Dtos;
using BellaSync.Application.Features.Expenses.Shared;
using BellaSync.Application.Features.Payments.Shared;
using BellaSync.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace BellaSync.Application.Features.Cash.GetDailyCashSummary;

public sealed class GetDailyCashSummaryHandler
    : IQueryHandler<GetDailyCashSummaryQuery, DailyCashSummaryResponse>
{
    // BellaSync opera solo en Colombia (UTC-5 todo el año).
    private static readonly TimeSpan ColombiaOffset = TimeSpan.FromHours(-5);

    private readonly IApplicationDbContext _db;
    private readonly IClock _clock;

    public GetDailyCashSummaryHandler(IApplicationDbContext db, IClock clock)
    {
        _db = db;
        _clock = clock;
    }

    public async Task<Result<DailyCashSummaryResponse>> HandleAsync(
        GetDailyCashSummaryQuery query, CancellationToken ct)
    {
        // Fecha objetivo en zona Colombia. Default = hoy.
        var date = query.Date ?? DateOnly.FromDateTime(
            _clock.UtcNow.Add(ColombiaOffset));

        // El día Colombia [00:00, 24:00) traducido a UTC para filtrar payments.
        var dayStartUtc = new DateTimeOffset(date.ToDateTime(TimeOnly.MinValue), ColombiaOffset)
            .UtcDateTime;
        var dayEndUtc = dayStartUtc.AddDays(1);

        // Traemos todos los payments del día con las navegaciones cargadas
        // para que el mapper pinte serviceName/stylistName.
        var payments = await _db.Payments
            .AsNoTracking()
            .Include(p => p.Appointment)!.ThenInclude(a => a!.Service)
            .Include(p => p.Appointment)!.ThenInclude(a => a!.Stylist)
            .Include(p => p.Appointment)!.ThenInclude(a => a!.Customer)
            .Include(p => p.RegisteredByUser)
            .Where(p => p.RegisteredAt >= dayStartUtc && p.RegisteredAt < dayEndUtc)
            .OrderBy(p => p.RegisteredAt)
            .ToListAsync(ct);

        // Vouchers validados HOY (por DecidedAt). Son anticipos que la
        // admin aprobó en este día — para la caja del día son revenue
        // que entró por transferencia con su banco. Se mergean al
        // breakdown por método para que la admin vea el TOTAL de plata
        // que entró hoy, no solo los pagos finales.
        var validatedVouchers = await _db.PaymentVouchers
            .AsNoTracking()
            .Where(v => v.Status == PaymentVoucherStatus.Validated
                     && v.DecidedAt != null
                     && v.DecidedAt >= dayStartUtc
                     && v.DecidedAt < dayEndUtc)
            .Select(v => new { v.ReportedAmount, v.Bank })
            .ToListAsync(ct);

        var depositsTotal = validatedVouchers.Sum(v => v.ReportedAmount.Amount);
        var depositsCount = validatedVouchers.Count;

        // Agregados en C# — los Money están con HasConversion y EF no
        // los traduce bien dentro de agrupaciones SQL (mismo problema
        // que tuvimos con vouchers). En memoria es trivial.
        // Total incluye TODO lo que entró: pagos finales + anticipos validados.
        var paymentsTotal = payments.Sum(p => p.Amount.Amount + p.Tip.Amount);
        var totalAmount = paymentsTotal + depositsTotal;
        var totalTips = payments.Sum(p => p.Tip.Amount);

        // Agrupar payments por método para el breakdown base.
        var paymentMethodGroups = payments
            .GroupBy(p => p.Method)
            .ToDictionary(g => g.Key, g => g.ToList());

        // Mergear vouchers en el grupo Transfer (anticipos por
        // transferencia son el caso casi universal en CO).
        var transferList = paymentMethodGroups.GetValueOrDefault(
            BellaSync.Domain.Entities.PaymentMethod.Transfer,
            new List<BellaSync.Domain.Entities.Payment>());

        var byMethod = new List<MethodBreakdownItem>();

        foreach (var (method, payList) in paymentMethodGroups)
        {
            var methodTotal = payList.Sum(p => p.Amount.Amount + p.Tip.Amount);
            var methodCount = payList.Count;

            // Si es Transfer, sumamos también los vouchers a este método.
            var voucherProviderRows = new List<ProviderBreakdownItem>();
            if (method == BellaSync.Domain.Entities.PaymentMethod.Transfer
                && validatedVouchers.Count > 0)
            {
                methodTotal += depositsTotal;
                methodCount += depositsCount;
                voucherProviderRows = validatedVouchers
                    .GroupBy(v => string.IsNullOrWhiteSpace(v.Bank) ? null : v.Bank)
                    .Select(vg => new ProviderBreakdownItem
                    {
                        Provider = vg.Key,
                        Count = vg.Count(),
                        Total = vg.Sum(v => v.ReportedAmount.Amount),
                    })
                    .ToList();
            }

            var paymentProviderRows = method == BellaSync.Domain.Entities.PaymentMethod.Cash
                ? new List<ProviderBreakdownItem>()
                : payList.GroupBy(p => p.Provider)
                    .Select(pg => new ProviderBreakdownItem
                    {
                        Provider = pg.Key,
                        Count = pg.Count(),
                        Total = pg.Sum(p => p.Amount.Amount + p.Tip.Amount),
                    })
                    .ToList();

            // Combinar provider rows de payments + vouchers, sumando por provider.
            var combinedProviders = paymentProviderRows
                .Concat(voucherProviderRows)
                .GroupBy(r => r.Provider)
                .Select(rg => new ProviderBreakdownItem
                {
                    Provider = rg.Key,
                    Count = rg.Sum(r => r.Count),
                    Total = rg.Sum(r => r.Total),
                })
                .OrderByDescending(r => r.Total)
                .ToList();

            byMethod.Add(new MethodBreakdownItem
            {
                Method = method.ToString(),
                Count = methodCount,
                Total = methodTotal,
                ByProvider = combinedProviders,
            });
        }

        // Si HAY vouchers pero NO había payments por Transfer todavía,
        // hay que crear la entrada de Transfer ex-nihilo.
        if (validatedVouchers.Count > 0
            && !paymentMethodGroups.ContainsKey(BellaSync.Domain.Entities.PaymentMethod.Transfer))
        {
            byMethod.Add(new MethodBreakdownItem
            {
                Method = BellaSync.Domain.Entities.PaymentMethod.Transfer.ToString(),
                Count = depositsCount,
                Total = depositsTotal,
                ByProvider = validatedVouchers
                    .GroupBy(v => string.IsNullOrWhiteSpace(v.Bank) ? null : v.Bank)
                    .Select(vg => new ProviderBreakdownItem
                    {
                        Provider = vg.Key,
                        Count = vg.Count(),
                        Total = vg.Sum(v => v.ReportedAmount.Amount),
                    })
                    .OrderByDescending(r => r.Total)
                    .ToList(),
            });
        }

        byMethod = byMethod.OrderByDescending(b => b.Total).ToList();

        // Egresos del día — mismo rango UTC.
        var expenses = await _db.Expenses
            .AsNoTracking()
            .Include(e => e.RegisteredByUser)
            .Where(e => e.RegisteredAt >= dayStartUtc && e.RegisteredAt < dayEndUtc)
            .OrderBy(e => e.RegisteredAt)
            .ToListAsync(ct);

        var totalExpenses = expenses.Sum(e => e.Amount.Amount);
        // Solo los egresos en efectivo afectan el arqueo de caja.
        var cashExpenses = expenses
            .Where(e => e.Method == PaymentMethod.Cash)
            .Sum(e => e.Amount.Amount);

        var response = new DailyCashSummaryResponse
        {
            Date = date.ToString("yyyy-MM-dd"),
            TotalAmount = totalAmount,
            TotalTips = totalTips,
            PaymentCount = payments.Count,
            ValidatedDepositsTotal = depositsTotal,
            ValidatedDepositsCount = depositsCount,
            ByMethod = byMethod,
            Payments = payments.Select(PaymentMapper.ToResponse).ToList(),
            TotalExpenses = totalExpenses,
            CashExpenses = cashExpenses,
            Expenses = expenses.Select(ExpenseMapper.ToResponse).ToList(),
        };

        return Result<DailyCashSummaryResponse>.Success(response);
    }
}
