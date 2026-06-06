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

    // Marcador del Bank en los vouchers que representan APLICACIÓN de
    // crédito interno (no plata nueva). Se usa al crear el voucher en
    // CreateAppointmentHandler.ApplyCustomerCreditsAsync.
    // Si en el futuro queremos discriminar por enum/columna en vez de
    // string mágico, este es el único lugar a tocar.
    private const string InternalCreditBankMarker = "Crédito interno";

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

        // Vouchers validados HOY (por DecidedAt). Separamos en 2 grupos:
        //   1. "Externos" — plata real que entró por transferencia bancaria.
        //      Se cuentan al TotalAmount y al breakdown por método.
        //   2. "Internos" — aplicación de crédito viejo (Bank = "Crédito interno").
        //      NO se cuentan al TotalAmount porque la plata ya había entrado
        //      antes; lo que pasa hoy es solo consumo de saldo.
        //
        // Cargamos con includes para poder exponer la lista completa en
        // ValidatedVouchersToday (drill-down en la UI de "Transacciones").
        var allValidatedVouchersTodayWithRefs = await _db.PaymentVouchers
            .AsNoTracking()
            .Include(v => v.Appointment).ThenInclude(a => a!.Customer)
            .Include(v => v.Appointment).ThenInclude(a => a!.Service)
            .Include(v => v.Appointment).ThenInclude(a => a!.Stylist)
            .Where(v => v.Status == PaymentVoucherStatus.Validated
                     && v.DecidedAt != null
                     && v.DecidedAt >= dayStartUtc
                     && v.DecidedAt < dayEndUtc)
            .ToListAsync(ct);

        // Versión simplificada para los sumadores que ya estaban.
        var allValidatedVouchersToday = allValidatedVouchersTodayWithRefs
            .Select(v => new { v.ReportedAmount, v.Bank })
            .ToList();

        var externalVouchers = allValidatedVouchersToday
            .Where(v => v.Bank != InternalCreditBankMarker)
            .ToList();
        var internalVouchers = allValidatedVouchersToday
            .Where(v => v.Bank == InternalCreditBankMarker)
            .ToList();

        // Lista detallada para la UI de transacciones.
        var validatedVoucherItems = allValidatedVouchersTodayWithRefs
            .OrderBy(v => v.DecidedAt)
            .Select(v => new ValidatedVoucherItem
            {
                VoucherId = v.Id,
                AppointmentId = v.AppointmentId,
                CustomerName = v.Appointment?.Customer?.FullName ?? string.Empty,
                ServiceName = v.Appointment?.Service?.Name ?? string.Empty,
                StylistName = v.Appointment?.Stylist?.FullName ?? string.Empty,
                Amount = v.ReportedAmount.Amount,
                Bank = v.Bank,
                IsInternalCredit = v.Bank == InternalCreditBankMarker,
                DecidedAt = v.DecidedAt ?? v.ReceivedAt,
            })
            .ToList();

        var depositsTotal = externalVouchers.Sum(v => v.ReportedAmount.Amount);
        var depositsCount = externalVouchers.Count;

        var internalCreditTotal = internalVouchers.Sum(v => v.ReportedAmount.Amount);
        var internalCreditCount = internalVouchers.Count;

        // Total del día = pagos directos + anticipos externos validados hoy.
        // EXCLUYE explícitamente los créditos internos (los muestra aparte).
        var paymentsTotal = payments.Sum(p => p.Amount.Amount + p.Tip.Amount);
        var totalAmount = paymentsTotal + depositsTotal;
        var totalTips = payments.Sum(p => p.Tip.Amount);

        // Forfeited HOY: cancelaciones tardías donde el salón retuvo el anticipo.
        // RefundResolvedAt es la hora de la cancelación (Forfeited se autoresuelve
        // al instante). Filtramos por ese campo dentro del rango del día.
        var forfeitedVouchers = await _db.PaymentVouchers
            .AsNoTracking()
            .Include(v => v.Appointment).ThenInclude(a => a!.Customer)
            .Include(v => v.Appointment).ThenInclude(a => a!.Service)
            .Where(v => v.RefundDecision == DepositRefundDecision.Forfeited
                     && v.RefundResolvedAt != null
                     && v.RefundResolvedAt >= dayStartUtc
                     && v.RefundResolvedAt < dayEndUtc)
            .ToListAsync(ct);

        var forfeitedItems = forfeitedVouchers.Select(v => new ForfeitedItem
        {
            VoucherId = v.Id,
            CustomerName = v.Appointment?.Customer?.FullName ?? string.Empty,
            ServiceName = v.Appointment?.Service?.Name ?? string.Empty,
            Amount = v.ReportedAmount.Amount,
            AppointmentStartAt = v.Appointment?.StartAt ?? default,
            CancelledAt = v.Appointment?.CancelledAt ?? v.RefundResolvedAt ?? default,
            CancellationReason = v.Appointment?.CancellationReason,
        }).ToList();

        var forfeitedTotal = forfeitedItems.Sum(f => f.Amount);
        var forfeitedCount = forfeitedItems.Count;

        // Agrupar payments por método para el breakdown base.
        var paymentMethodGroups = payments
            .GroupBy(p => p.Method)
            .ToDictionary(g => g.Key, g => g.ToList());

        var byMethod = new List<MethodBreakdownItem>();

        foreach (var (method, payList) in paymentMethodGroups)
        {
            var methodTotal = payList.Sum(p => p.Amount.Amount + p.Tip.Amount);
            var methodCount = payList.Count;

            // Si es Transfer, sumamos también los vouchers EXTERNOS a este método.
            // Los internos NO se mergean acá porque no representan plata bancaria.
            var voucherProviderRows = new List<ProviderBreakdownItem>();
            if (method == PaymentMethod.Transfer && externalVouchers.Count > 0)
            {
                methodTotal += depositsTotal;
                methodCount += depositsCount;
                voucherProviderRows = externalVouchers
                    .GroupBy(v => string.IsNullOrWhiteSpace(v.Bank) ? null : v.Bank)
                    .Select(vg => new ProviderBreakdownItem
                    {
                        Provider = vg.Key,
                        Count = vg.Count(),
                        Total = vg.Sum(v => v.ReportedAmount.Amount),
                    })
                    .ToList();
            }

            var paymentProviderRows = method == PaymentMethod.Cash
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

        // Si HAY vouchers externos pero NO había payments por Transfer todavía,
        // hay que crear la entrada de Transfer ex-nihilo.
        if (externalVouchers.Count > 0
            && !paymentMethodGroups.ContainsKey(PaymentMethod.Transfer))
        {
            byMethod.Add(new MethodBreakdownItem
            {
                Method = PaymentMethod.Transfer.ToString(),
                Count = depositsCount,
                Total = depositsTotal,
                ByProvider = externalVouchers
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
            // Conteo de movimientos visibles: pagos + vouchers externos.
            // Internos NO porque no son movimientos de plata.
            PaymentCount = payments.Count + depositsCount,
            ValidatedDepositsTotal = depositsTotal,
            ValidatedDepositsCount = depositsCount,
            InternalCreditTotal = internalCreditTotal,
            InternalCreditCount = internalCreditCount,
            ForfeitedTodayTotal = forfeitedTotal,
            ForfeitedTodayCount = forfeitedCount,
            ForfeitedToday = forfeitedItems,
            ByMethod = byMethod,
            Payments = payments.Select(PaymentMapper.ToResponse).ToList(),
            ValidatedVouchersToday = validatedVoucherItems,
            TotalExpenses = totalExpenses,
            CashExpenses = cashExpenses,
            Expenses = expenses.Select(ExpenseMapper.ToResponse).ToList(),
        };

        return Result<DailyCashSummaryResponse>.Success(response);
    }
}
