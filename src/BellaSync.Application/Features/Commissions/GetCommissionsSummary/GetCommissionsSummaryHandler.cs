using BellaSync.Application.Common.Errors;
using BellaSync.Application.Common.Handlers;
using BellaSync.Application.Common.Interfaces;
using BellaSync.Application.Common.Results;
using BellaSync.Application.Features.Commissions.Dtos;
using BellaSync.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace BellaSync.Application.Features.Commissions.GetCommissionsSummary;

/// <summary>
/// Calcula las comisiones devengadas por cada estilista en el rango.
///
/// Reglas de negocio:
///
///   - Solo citas Completed o InProgress generan comisión. Las
///     Cancelled / NoShow no — aunque el salón se haya quedado con el
///     anticipo (forfeiture), el estilista NO trabajó esa cita.
///
///   - Base de comisión = TODA la plata cobrada por la cita
///     (Payments + Vouchers Validated), no solo el saldo final.
///     Antes el handler sumaba solo Payments, lo que subestimaba la
///     comisión en cualquier cita con anticipo. Ejemplo: cita $150k,
///     anticipo $80k + saldo $70k:
///       - Antes: comisión sobre $70k = $21k (mal)
///       - Ahora: comisión sobre $150k = $45k (correcto)
///
///   - Período: filtramos por Appointment.StartAt (fecha del servicio),
///     no por fecha del pago. "Comisiones de julio" = "servicios
///     realizados en julio", que es el mental model natural del salón.
///     Si el saldo se cobra después (ej: cita el 30/jul, saldo cobrado
///     el 1/ago), igual cuenta en julio.
///
/// Como Money * Percentage no se traduce a SQL (mismo bug histórico),
/// traemos en memoria y agregamos en C#.
/// </summary>
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

        // 1) Citas atendidas en el rango (Completed o InProgress).
        //    Cancelled/NoShow NO generan comisión aunque haya forfeiture.
        var attendedAppts = await _db.Appointments
            .AsNoTracking()
            .Include(a => a.Service)
            .Include(a => a.Stylist)
            .Where(a => a.StartAt >= dayStartUtc
                     && a.StartAt < dayEndUtc
                     && (a.Status == AppointmentStatus.Completed
                         || a.Status == AppointmentStatus.InProgress))
            .ToListAsync(ct);

        if (attendedAppts.Count == 0)
        {
            // Sigue habiendo payouts en el rango aunque no haya nuevas
            // atendidas — los pintamos abajo igual.
        }

        var attendedIds = attendedAppts.Select(a => a.Id).ToList();

        // 2) Payments asociados a esas citas (saldos finales + propinas).
        var payments = await _db.Payments
            .AsNoTracking()
            .Where(p => attendedIds.Contains(p.AppointmentId))
            .Select(p => new { p.AppointmentId, p.Amount, p.Tip })
            .ToListAsync(ct);

        var paymentsByAppt = payments
            .GroupBy(p => p.AppointmentId)
            .ToDictionary(g => g.Key, g => g.Sum(p => p.Amount.Amount + p.Tip.Amount));

        // 3) Vouchers Validated asociados (anticipos que entraron a caja).
        var vouchers = await _db.PaymentVouchers
            .AsNoTracking()
            .Where(v => attendedIds.Contains(v.AppointmentId)
                     && v.Status == PaymentVoucherStatus.Validated)
            .Select(v => new { v.AppointmentId, v.ReportedAmount })
            .ToListAsync(ct);

        var depositsByAppt = vouchers
            .GroupBy(v => v.AppointmentId)
            .ToDictionary(g => g.Key, g => g.Sum(v => v.ReportedAmount.Amount));

        // 4) Para cada cita atendida, calcular base + comisión.
        //    Base = sum(Payments) + sum(Validated Vouchers).
        //    Comisión = base * Service.CommissionPercentage / 100.
        var perAppt = attendedAppts
            .Where(a => a.Service is not null && a.Stylist is not null)
            .Select(a =>
            {
                var paid = paymentsByAppt.GetValueOrDefault(a.Id, 0m);
                var deposit = depositsByAppt.GetValueOrDefault(a.Id, 0m);
                var base_ = paid + deposit;
                var pct = a.Service!.CommissionPercentage.Value;
                var commission = base_ * pct / 100m;
                return new
                {
                    a.Id,
                    Stylist = a.Stylist!,
                    Base = base_,
                    Commission = commission,
                };
            })
            .ToList();

        // 5) Agrupar por estilista.
        var grouped = perAppt
            .GroupBy(x => x.Stylist)
            .Select(g => new
            {
                Stylist = g.Key,
                PaymentsCount = g.Count(),  // citas atendidas (no pagos)
                CobradoTotal = g.Sum(x => x.Base),
                CommissionEarned = g.Sum(x => x.Commission),
            })
            .ToList();

        // 6) Payouts cuyo período se solapa con el rango.
        var payouts = await _db.CommissionPayouts
            .AsNoTracking()
            .Where(cp => cp.PeriodTo >= query.From && cp.PeriodFrom <= query.To)
            .ToListAsync(ct);

        var paidByStylist = payouts
            .GroupBy(cp => cp.StylistId)
            .ToDictionary(g => g.Key, g => g.Sum(cp => cp.Amount.Amount));

        // 7) Filas combinadas (estilistas con citas atendidas + con payouts).
        var stylistIdsConCitas = grouped.Select(g => g.Stylist.Id).ToHashSet();
        var stylistIdsConPayouts = paidByStylist.Keys.ToHashSet();
        var allStylistIds = stylistIdsConCitas.Union(stylistIdsConPayouts).ToList();

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
