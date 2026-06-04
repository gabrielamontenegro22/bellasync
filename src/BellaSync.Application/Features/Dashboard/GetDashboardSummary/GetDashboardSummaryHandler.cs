using BellaSync.Application.Common;
using BellaSync.Application.Common.Handlers;
using BellaSync.Application.Common.Interfaces;
using BellaSync.Application.Common.Results;
using BellaSync.Application.Features.Dashboard.Dtos;
using BellaSync.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace BellaSync.Application.Features.Dashboard.GetDashboardSummary;

/// <summary>
/// Compone el snapshot del dashboard en una sola query. Materializa
/// los conteos del día actual + semana actual + pendientes globales.
///
/// Reglas:
///   - "Hoy" = día Colombia (no UTC) — usamos ColombiaTime helper.
///   - Semana = lunes a domingo de la semana actual (estándar CO).
///   - Revenue del día/semana = SOLO payments (no incluye vouchers
///     validados acá; los reportes principales ya manejan ese caso).
///   - Pendientes = vouchers en estado Pending del tenant.
///   - NextAppointment = la próxima cita Confirmed/Pending desde "ahora"
///     hasta fin del día.
/// </summary>
public sealed class GetDashboardSummaryHandler
    : IQueryHandler<GetDashboardSummaryQuery, DashboardSummaryResponse>
{
    private readonly IApplicationDbContext _db;
    private readonly IClock _clock;

    public GetDashboardSummaryHandler(IApplicationDbContext db, IClock clock)
    {
        _db = db;
        _clock = clock;
    }

    public async Task<Result<DashboardSummaryResponse>> HandleAsync(
        GetDashboardSummaryQuery query, CancellationToken ct)
    {
        var utcNow = _clock.UtcNow;
        var today = ColombiaTime.TodayFor(utcNow);
        var (todayStartUtc, todayEndUtc) = ColombiaTime.DayRangeUtc(today);

        // Semana lunes-domingo en hora Colombia.
        var dotnetDayOfWeek = (int)today.DayOfWeek;
        var daysFromMonday = (dotnetDayOfWeek + 6) % 7;  // Domingo=6, Lunes=0
        var weekStart = today.AddDays(-daysFromMonday);
        var weekEnd = weekStart.AddDays(7);
        var (weekStartUtc, weekEndUtc) = (
            ColombiaTime.DayRangeUtc(weekStart).startUtc,
            ColombiaTime.DayRangeUtc(weekEnd).startUtc);

        // ===== Citas del día =====
        var todayAppts = await _db.Appointments
            .AsNoTracking()
            .Where(a => a.StartAt >= todayStartUtc && a.StartAt < todayEndUtc)
            .Select(a => new
            {
                a.Id,
                a.CustomerId,
                a.ServiceId,
                a.StylistId,
                a.StartAt,
                a.EndAt,
                a.Status,
            })
            .ToListAsync(ct);

        var todayAppointmentsCount = todayAppts.Count(a =>
            a.Status != AppointmentStatus.Cancelled);
        var todayCompletedCount = todayAppts.Count(a =>
            a.Status == AppointmentStatus.Completed);
        var todayPendingCount = todayAppts.Count(a =>
            a.Status == AppointmentStatus.Pending
            || a.Status == AppointmentStatus.Confirmed
            || a.Status == AppointmentStatus.InProgress);

        // ===== Próxima cita (desde ahora hasta fin del día) =====
        NextAppointmentDto? nextAppt = null;
        var nextRaw = todayAppts
            .Where(a => a.StartAt >= utcNow
                     && (a.Status == AppointmentStatus.Pending
                         || a.Status == AppointmentStatus.Confirmed
                         || a.Status == AppointmentStatus.InProgress))
            .OrderBy(a => a.StartAt)
            .FirstOrDefault();

        if (nextRaw is not null)
        {
            var customer = await _db.Customers
                .AsNoTracking()
                .Where(c => c.Id == nextRaw.CustomerId)
                .Select(c => c.FullName)
                .FirstOrDefaultAsync(ct);
            var service = await _db.Services
                .AsNoTracking()
                .Where(s => s.Id == nextRaw.ServiceId)
                .Select(s => s.Name)
                .FirstOrDefaultAsync(ct);
            var stylist = await _db.Stylists
                .AsNoTracking()
                .Where(s => s.Id == nextRaw.StylistId)
                .Select(s => new { s.FullName, s.Color })
                .FirstOrDefaultAsync(ct);

            nextAppt = new NextAppointmentDto
            {
                Id = nextRaw.Id,
                CustomerName = customer ?? "—",
                ServiceName = service ?? "—",
                StylistName = stylist?.FullName ?? "—",
                StylistColor = stylist?.Color,
                StartAt = nextRaw.StartAt,
                EndAt = nextRaw.EndAt,
                Status = nextRaw.Status.ToString(),
            };
        }

        // ===== Revenue del día =====
        // Materializamos a memoria para sumar Money VOs (no se traduce en SQL).
        var todayPayments = await _db.Payments
            .AsNoTracking()
            .Where(p => p.RegisteredAt >= todayStartUtc && p.RegisteredAt < todayEndUtc)
            .Select(p => new { p.Amount, p.Tip })
            .ToListAsync(ct);
        var todayRevenue = todayPayments.Sum(p => p.Amount.Amount + p.Tip.Amount);

        // ===== Semana =====
        var weekApptsCount = await _db.Appointments
            .AsNoTracking()
            .CountAsync(a => a.StartAt >= weekStartUtc
                          && a.StartAt < weekEndUtc
                          && (a.Status == AppointmentStatus.Completed
                              || a.Status == AppointmentStatus.InProgress
                              || a.Status == AppointmentStatus.Confirmed
                              || a.Status == AppointmentStatus.Pending), ct);

        var weekPayments = await _db.Payments
            .AsNoTracking()
            .Where(p => p.RegisteredAt >= weekStartUtc && p.RegisteredAt < weekEndUtc)
            .Select(p => new { p.Amount, p.Tip })
            .ToListAsync(ct);
        var weekRevenue = weekPayments.Sum(p => p.Amount.Amount + p.Tip.Amount);

        // ===== Pendientes para badges =====
        var pendingVouchers = await _db.PaymentVouchers
            .CountAsync(v => v.Status == PaymentVoucherStatus.Pending, ct);

        var cashClosingExists = await _db.CashClosings
            .AsNoTracking()
            .AnyAsync(c => c.ClosedDate == today, ct);

        return Result<DashboardSummaryResponse>.Success(new DashboardSummaryResponse
        {
            Today = today,
            TodayAppointmentsCount = todayAppointmentsCount,
            TodayCompletedCount = todayCompletedCount,
            TodayPendingCount = todayPendingCount,
            TodayRevenue = todayRevenue,
            NextAppointment = nextAppt,
            WeekAppointmentsCount = weekApptsCount,
            WeekRevenue = weekRevenue,
            PendingVouchersCount = pendingVouchers,
            CashClosingPending = !cashClosingExists && todayPayments.Count > 0,
        });
    }
}
