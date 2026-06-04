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
            .Where(p => p.RegisteredAt >= dayStartUtc && p.RegisteredAt < dayEndUtc)
            .OrderBy(p => p.RegisteredAt)
            .ToListAsync(ct);

        // Agregados en C# — los Money están con HasConversion y EF no
        // los traduce bien dentro de agrupaciones SQL (mismo problema
        // que tuvimos con vouchers). En memoria es trivial.
        var totalAmount = payments.Sum(p => p.Amount.Amount + p.Tip.Amount);
        var totalTips = payments.Sum(p => p.Tip.Amount);

        var byMethod = payments
            .GroupBy(p => p.Method)
            .Select(g => new MethodBreakdownItem
            {
                Method = g.Key.ToString(),
                Count = g.Count(),
                Total = g.Sum(p => p.Amount.Amount + p.Tip.Amount),
                // Sub-desglose por banco/billetera/marca. Para Cash queda
                // vacío porque agrupar por Provider=null no aporta.
                ByProvider = g.Key == BellaSync.Domain.Entities.PaymentMethod.Cash
                    ? new List<ProviderBreakdownItem>()
                    : g.GroupBy(p => p.Provider)
                        .Select(pg => new ProviderBreakdownItem
                        {
                            Provider = pg.Key,
                            Count = pg.Count(),
                            Total = pg.Sum(p => p.Amount.Amount + p.Tip.Amount),
                        })
                        .OrderByDescending(p => p.Total)
                        .ToList(),
            })
            // Orden: por monto descendente — los métodos con más plata arriba.
            .OrderByDescending(b => b.Total)
            .ToList();

        // Egresos del día — mismo rango UTC.
        var expenses = await _db.Expenses
            .AsNoTracking()
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
            ByMethod = byMethod,
            Payments = payments.Select(PaymentMapper.ToResponse).ToList(),
            TotalExpenses = totalExpenses,
            CashExpenses = cashExpenses,
            Expenses = expenses.Select(ExpenseMapper.ToResponse).ToList(),
        };

        return Result<DailyCashSummaryResponse>.Success(response);
    }
}
