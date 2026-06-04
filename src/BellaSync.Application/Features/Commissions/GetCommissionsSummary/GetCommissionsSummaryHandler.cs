using BellaSync.Application.Common.Errors;
using BellaSync.Application.Common.Handlers;
using BellaSync.Application.Common.Interfaces;
using BellaSync.Application.Common.Results;
using BellaSync.Application.Features.Commissions.Dtos;
using Microsoft.EntityFrameworkCore;

namespace BellaSync.Application.Features.Commissions.GetCommissionsSummary;

public sealed class GetCommissionsSummaryHandler
    : IQueryHandler<GetCommissionsSummaryQuery, CommissionsSummaryResponse>
{
    private static readonly TimeSpan ColombiaOffset = TimeSpan.FromHours(-5);

    private readonly IApplicationDbContext _db;

    public GetCommissionsSummaryHandler(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<Result<CommissionsSummaryResponse>> HandleAsync(
        GetCommissionsSummaryQuery query, CancellationToken ct)
    {
        if (query.From > query.To)
            return ApplicationError.Validation("commissions.bad_range", "From debe ser <= To.");

        var dayStartUtc = new DateTimeOffset(query.From.ToDateTime(TimeOnly.MinValue), ColombiaOffset).UtcDateTime;
        var dayEndUtc   = new DateTimeOffset(query.To.AddDays(1).ToDateTime(TimeOnly.MinValue), ColombiaOffset).UtcDateTime;

        // 1) Pagos del rango con join a appointment + service + stylist.
        //    Traemos en memoria porque la comisión derivada involucra
        //    multiplicar Money * Percentage (VOs con HasConversion) y
        //    EF no lo traduce bien en GROUP BY (mismo problema histórico).
        var payments = await _db.Payments
            .AsNoTracking()
            .Include(p => p.Appointment)!.ThenInclude(a => a!.Service)
            .Include(p => p.Appointment)!.ThenInclude(a => a!.Stylist)
            .Where(p => p.RegisteredAt >= dayStartUtc && p.RegisteredAt < dayEndUtc)
            .ToListAsync(ct);

        // 2) Agrupar por estilista en C#.
        var grouped = payments
            .Where(p => p.Appointment?.Stylist is not null && p.Appointment?.Service is not null)
            .GroupBy(p => p.Appointment!.Stylist!)
            .Select(g => new
            {
                Stylist = g.Key,
                PaymentsCount = g.Count(),
                CobradoTotal = g.Sum(p => p.Amount.Amount),
                // Comisión = sumatoria(amount * pct / 100) por cada pago.
                CommissionEarned = g.Sum(p =>
                    p.Amount.Amount * p.Appointment!.Service!.CommissionPercentage.Value / 100m),
            })
            .ToList();

        // 3) Payouts cuyo período se solapa con el rango. Para v1
        //    "se solapa" = period_to >= From AND period_from <= To.
        var payouts = await _db.CommissionPayouts
            .AsNoTracking()
            .Where(cp => cp.PeriodTo >= query.From && cp.PeriodFrom <= query.To)
            .ToListAsync(ct);

        var paidByStylist = payouts
            .GroupBy(cp => cp.StylistId)
            .ToDictionary(g => g.Key, g => g.Sum(cp => cp.Amount.Amount));

        // 4) Construir filas. Incluimos también estilistas que SOLO
        //    tienen payouts en el rango (sin pagos nuevos) — para que
        //    aparezcan en el historial con su "paid".
        var stylistIdsConPagos = grouped.Select(g => g.Stylist.Id).ToHashSet();
        var stylistIdsConPayouts = paidByStylist.Keys.ToHashSet();
        var allStylistIds = stylistIdsConPagos.Union(stylistIdsConPayouts).ToList();

        var stylistsLookup = await _db.Stylists
            .AsNoTracking()
            .Where(s => allStylistIds.Contains(s.Id))
            .ToDictionaryAsync(s => s.Id, ct);

        var rows = allStylistIds
            .Select(id =>
            {
                var g = grouped.FirstOrDefault(x => x.Stylist.Id == id);
                var stylist = g?.Stylist ?? stylistsLookup[id];
                var earned = g?.CommissionEarned ?? 0m;
                var paid = paidByStylist.TryGetValue(id, out var p) ? p : 0m;
                return new StylistCommissionRow
                {
                    StylistId = stylist.Id,
                    StylistName = stylist.FullName,
                    StylistColor = stylist.Color,
                    PaymentsCount = g?.PaymentsCount ?? 0,
                    CobradoTotal = g?.CobradoTotal ?? 0m,
                    CommissionEarned = earned,
                    AlreadyPaidInRange = paid,
                    Pending = Math.Max(0m, earned - paid),
                };
            })
            // Orden: por pending descendente (los que más se le debe arriba),
            // luego por earned, luego alfabético.
            .OrderByDescending(r => r.Pending)
            .ThenByDescending(r => r.CommissionEarned)
            .ThenBy(r => r.StylistName)
            .ToList();

        var response = new CommissionsSummaryResponse
        {
            From = query.From.ToString("yyyy-MM-dd"),
            To = query.To.ToString("yyyy-MM-dd"),
            Stylists = rows,
            TotalEarned = rows.Sum(r => r.CommissionEarned),
            TotalPaid = rows.Sum(r => r.AlreadyPaidInRange),
            TotalPending = rows.Sum(r => r.Pending),
        };

        return Result<CommissionsSummaryResponse>.Success(response);
    }
}
