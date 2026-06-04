using BellaSync.Application.Common.Errors;
using BellaSync.Application.Common.Handlers;
using BellaSync.Application.Common.Interfaces;
using BellaSync.Application.Common.Results;
using BellaSync.Application.Features.Reports.Dtos;
using BellaSync.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace BellaSync.Application.Features.Reports.GetReportsSummary;

/// <summary>
/// Calcula el snapshot de reports para un período. Estrategia:
///   - Una sola pasada por cada agregación SQL — varias queries pero
///     cada una específica, todas usando índices por tenant_id + start_at.
///   - Revenue = sum(Payment.Total) de los Payments cuyo Appointment
///     cayó dentro del rango (no se cuenta cuándo se cobró el pago en
///     sí mismo, sino cuándo se prestó el servicio). Eso refleja
///     "ingresos del período" en sentido contable.
///   - Citas: las que tienen StartAt dentro del rango Y status no es
///     Cancelled/NoShow.
///   - Top services/stylists: ordenado por Revenue desc, limit 5.
///   - Weekly trend: agrupa por inicio-de-semana ISO (lunes).
///
/// Convertimos las fechas DateOnly a DateTime UTC asumiendo que el
/// rango es en hora Colombia (-5): From=00:00 local → 05:00 UTC,
/// To+1día=00:00 local → 05:00 UTC del día siguiente (rango exclusive
/// del end). Esto matchea "del 1 al 30 de junio" como rango natural.
/// </summary>
public sealed class GetReportsSummaryHandler
    : IQueryHandler<GetReportsSummaryQuery, ReportsSummaryResponse>
{
    private static readonly TimeSpan ColombiaOffset = TimeSpan.FromHours(-5);

    private readonly IApplicationDbContext _db;
    private readonly ICurrentTenantService _currentTenant;

    public GetReportsSummaryHandler(
        IApplicationDbContext db,
        ICurrentTenantService currentTenant)
    {
        _db = db;
        _currentTenant = currentTenant;
    }

    public async Task<Result<ReportsSummaryResponse>> HandleAsync(
        GetReportsSummaryQuery query, CancellationToken ct)
    {
        if (query.From > query.To)
            return ApplicationError.Validation(
                "reports.invalid_range",
                "La fecha 'desde' no puede ser posterior a 'hasta'.");

        var totalDays = query.To.DayNumber - query.From.DayNumber + 1;
        if (totalDays > 366)
            return ApplicationError.Validation(
                "reports.range_too_large",
                "El rango no puede superar 1 año.");

        var tenantId = _currentTenant.TenantId;

        // Rango UTC inclusive-exclusive: [From 00:00 CO, To+1día 00:00 CO).
        var startUtc = query.From.ToDateTime(TimeOnly.MinValue) - ColombiaOffset;
        var endUtc = query.To.AddDays(1).ToDateTime(TimeOnly.MinValue) - ColombiaOffset;

        // ===== Citas válidas del período (las que cuentan para todo) =====
        var validAppointments = _db.Appointments
            .Where(a => a.TenantId == tenantId
                     && a.StartAt >= startUtc
                     && a.StartAt < endUtc
                     && a.Status != AppointmentStatus.Cancelled
                     && a.Status != AppointmentStatus.NoShow);

        var appointmentsCount = await validAppointments.CountAsync(ct);

        // ===== Revenue total (pagos asociados a citas del período) =====
        // Join in-memory porque Payment no tiene un FK a Tenant directo
        // visible, pero está en el mismo contexto multi-tenant. Filtra
        // por las citas del período.
        var apptIds = await validAppointments.Select(a => a.Id).ToListAsync(ct);

        var totalRevenue = await _db.Payments
            .Where(p => p.TenantId == tenantId && apptIds.Contains(p.AppointmentId))
            .SumAsync(p => (decimal?)(p.Amount.Amount + p.Tip.Amount), ct) ?? 0m;

        // ===== Período anterior, para % de cambio =====
        var prevEndUtc = startUtc;
        var prevStartUtc = startUtc - (endUtc - startUtc);

        var prevApptIds = await _db.Appointments
            .Where(a => a.TenantId == tenantId
                     && a.StartAt >= prevStartUtc
                     && a.StartAt < prevEndUtc
                     && a.Status != AppointmentStatus.Cancelled
                     && a.Status != AppointmentStatus.NoShow)
            .Select(a => a.Id)
            .ToListAsync(ct);

        var prevRevenue = await _db.Payments
            .Where(p => p.TenantId == tenantId && prevApptIds.Contains(p.AppointmentId))
            .SumAsync(p => (decimal?)(p.Amount.Amount + p.Tip.Amount), ct) ?? 0m;

        double? revenueChangePct = prevRevenue > 0
            ? (double)((totalRevenue - prevRevenue) / prevRevenue) * 100.0
            : null;

        // ===== Clientes nuevos en el período =====
        var newCustomersCount = await _db.Customers
            .Where(c => c.TenantId == tenantId
                     && c.CreatedAt >= startUtc
                     && c.CreatedAt < endUtc)
            .CountAsync(ct);

        // ===== Ticket promedio =====
        var avgTicket = appointmentsCount > 0
            ? totalRevenue / appointmentsCount
            : 0m;

        // ===== Top 5 servicios por revenue =====
        // Agrupa Payments por su Appointment.ServiceId, suma totales.
        var servicePayments = await _db.Payments
            .Where(p => p.TenantId == tenantId && apptIds.Contains(p.AppointmentId))
            .Join(_db.Appointments,
                p => p.AppointmentId,
                a => a.Id,
                (p, a) => new { a.ServiceId, Amount = p.Amount.Amount + p.Tip.Amount })
            .GroupBy(x => x.ServiceId)
            .Select(g => new
            {
                ServiceId = g.Key,
                Revenue = g.Sum(x => x.Amount),
                Cnt = g.Count(),
            })
            .OrderByDescending(x => x.Revenue)
            .Take(5)
            .ToListAsync(ct);

        var serviceIds = servicePayments.Select(s => s.ServiceId).ToList();
        var serviceNames = await _db.Services
            .Where(s => serviceIds.Contains(s.Id))
            .Select(s => new { s.Id, s.Name })
            .ToDictionaryAsync(s => s.Id, s => s.Name, ct);

        var topServices = servicePayments
            .Select(s => new TopServiceRow
            {
                ServiceId = s.ServiceId,
                ServiceName = serviceNames.GetValueOrDefault(s.ServiceId, "?"),
                AppointmentsCount = s.Cnt,
                Revenue = s.Revenue,
            })
            .ToList();

        // ===== Top 5 estilistas por revenue =====
        var stylistPayments = await _db.Payments
            .Where(p => p.TenantId == tenantId && apptIds.Contains(p.AppointmentId))
            .Join(_db.Appointments,
                p => p.AppointmentId,
                a => a.Id,
                (p, a) => new { a.StylistId, Amount = p.Amount.Amount + p.Tip.Amount })
            .GroupBy(x => x.StylistId)
            .Select(g => new
            {
                StylistId = g.Key,
                Revenue = g.Sum(x => x.Amount),
                Cnt = g.Count(),
            })
            .OrderByDescending(x => x.Revenue)
            .Take(5)
            .ToListAsync(ct);

        var stylistIds = stylistPayments.Select(s => s.StylistId).ToList();
        var stylistInfo = await _db.Stylists
            .Where(s => stylistIds.Contains(s.Id))
            .Select(s => new { s.Id, s.FullName, s.Color })
            .ToDictionaryAsync(s => s.Id, ct);

        var topStylists = stylistPayments
            .Select(s => new TopStylistRow
            {
                StylistId = s.StylistId,
                StylistName = stylistInfo.GetValueOrDefault(s.StylistId)?.FullName ?? "?",
                StylistColor = stylistInfo.GetValueOrDefault(s.StylistId)?.Color,
                AppointmentsCount = s.Cnt,
                Revenue = s.Revenue,
            })
            .ToList();

        // ===== Tendencia semanal de ingresos =====
        // SQL no agrupa fácil por "semana ISO" cross-PG así que traemos
        // los payments del período con su startAt y agrupamos en memoria.
        // El volumen razonable (~cientos de pagos/mes) lo tolera.
        var paymentRows = await _db.Payments
            .Where(p => p.TenantId == tenantId && apptIds.Contains(p.AppointmentId))
            .Join(_db.Appointments,
                p => p.AppointmentId,
                a => a.Id,
                (p, a) => new { a.StartAt, Amount = p.Amount.Amount + p.Tip.Amount })
            .ToListAsync(ct);

        var weekly = paymentRows
            .GroupBy(p => WeekStartUtc(p.StartAt))
            .Select(g => new WeeklyRevenuePoint
            {
                WeekStart = DateOnly.FromDateTime((g.Key + ColombiaOffset).Date),
                Revenue = g.Sum(x => x.Amount),
            })
            .OrderBy(p => p.WeekStart)
            .ToList();

        // ===== Nuevos vs recurrentes en el período =====
        // "Nueva visita" = el customer no tenía citas antes de startUtc.
        var apptsWithCustomer = await validAppointments
            .Select(a => new { a.Id, a.CustomerId })
            .ToListAsync(ct);

        var customerIdsInPeriod = apptsWithCustomer.Select(a => a.CustomerId).Distinct().ToList();

        var customersWithPriorVisit = await _db.Appointments
            .Where(a => a.TenantId == tenantId
                     && customerIdsInPeriod.Contains(a.CustomerId)
                     && a.StartAt < startUtc
                     && a.Status != AppointmentStatus.Cancelled
                     && a.Status != AppointmentStatus.NoShow)
            .Select(a => a.CustomerId)
            .Distinct()
            .ToListAsync(ct);

        var returningSet = customersWithPriorVisit.ToHashSet();
        var newCustomerAppts = apptsWithCustomer.Count(a => !returningSet.Contains(a.CustomerId));
        var returningAppts = apptsWithCustomer.Count - newCustomerAppts;

        return Result<ReportsSummaryResponse>.Success(new ReportsSummaryResponse
        {
            From = query.From,
            To = query.To,
            TotalRevenue = totalRevenue,
            AppointmentsCount = appointmentsCount,
            AverageTicket = avgTicket,
            NewCustomersCount = newCustomersCount,
            RevenueChangePct = revenueChangePct,
            TopServices = topServices,
            TopStylists = topStylists,
            WeeklyRevenue = weekly,
            NewCustomerAppointments = newCustomerAppts,
            ReturningCustomerAppointments = returningAppts,
        });
    }

    /// <summary>
    /// Devuelve el lunes 00:00 UTC de la semana ISO que contiene la fecha
    /// (interpretando la fecha en hora Colombia). Usado para agrupar por
    /// semana en la tendencia.
    /// </summary>
    private static DateTime WeekStartUtc(DateTime dtUtc)
    {
        var local = dtUtc + ColombiaOffset;
        var diff = (7 + (int)local.DayOfWeek - (int)DayOfWeek.Monday) % 7;
        var monday = local.AddDays(-diff).Date;
        return monday - ColombiaOffset;
    }
}
