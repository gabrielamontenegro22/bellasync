using BellaSync.Application.Common.Errors;
using BellaSync.Application.Common.Handlers;
using BellaSync.Application.Common.Interfaces;
using BellaSync.Application.Common.Results;
using BellaSync.Application.Features.Reports.Dtos;
using BellaSync.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace BellaSync.Application.Features.Reports.GetReportsSummary;

/// <summary>
/// Calcula el snapshot de reports para un período. Devuelve TODO lo que
/// el dashboard /reportes (mockup v2) necesita en una sola call:
///
///   - 5 KPIs con delta vs período anterior (ingresos / citas / ticket /
///     no-show / clientas nuevas)
///   - Tendencia diaria de ingresos (un punto por día — para area chart)
///   - Dona de métodos de pago (Cash/Transfer/Card/Other)
///   - Top 5 servicios + Top 5 estilistas (con ocupación + no-shows)
///   - Embudo Solicitadas → Confirmadas → Atendidas → No-show
///   - Split nuevos vs recurrentes
///   - Insight dinámico en español ("Subiste 13% en ingresos…")
///
/// Estrategia técnica:
///   - Materializamos los pagos del período a memoria una sola vez —
///     EF Core no traduce p.Amount.Amount + p.Tip.Amount a SQL porque
///     el value converter Money↔decimal no compone dentro de SUM/GROUP BY.
///   - Las agregaciones (top services, stylists, daily revenue, payment
///     methods) se hacen en memoria sobre ese set.
///   - Cuento del embudo SÍ se hace en SQL — son COUNT(*) WHERE status,
///     no involucran Money VOs.
///   - Ocupación del estilista usa horario del salón si está configurado,
///     sino fallback de 11h × días abiertos.
///
/// Fechas: rango [From 00:00 CO, To+1día 00:00 CO). SpecifyKind(Utc)
/// para que Npgsql acepte como timestamp with time zone.
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
        var (startUtc, endUtc) = ToUtcRange(query.From, query.To);
        var (prevStartUtc, prevEndUtc) = (startUtc - (endUtc - startUtc), startUtc);

        // ===== Citas válidas (atendidas o programadas) del período actual =====
        var validApptsQ = _db.Appointments
            .Where(a => a.TenantId == tenantId
                     && a.StartAt >= startUtc
                     && a.StartAt < endUtc
                     && a.Status != AppointmentStatus.Cancelled
                     && a.Status != AppointmentStatus.NoShow);

        var appointmentsCount = await validApptsQ.CountAsync(ct);

        // Metadata por cita (para joins en memoria + ocupación + nuevos)
        var apptMeta = await validApptsQ
            .Select(a => new
            {
                a.Id, a.CustomerId, a.ServiceId, a.StylistId,
                a.StartAt, a.EndAt,
            })
            .ToListAsync(ct);

        var apptMetaById = apptMeta.ToDictionary(a => a.Id);

        // ===== Revenue actual y previo =====
        var apptIds = apptMeta.Select(a => a.Id).ToList();

        var paymentsCurrent = await _db.Payments
            .Where(p => p.TenantId == tenantId && apptIds.Contains(p.AppointmentId))
            .Select(p => new { p.AppointmentId, p.Method, p.Amount, p.Tip })
            .ToListAsync(ct);

        var totalRevenue = paymentsCurrent.Sum(p => p.Amount.Amount + p.Tip.Amount);

        // Período anterior — solo citas válidas + sus revenues, no necesitamos meta.
        var prevApptIds = await _db.Appointments
            .Where(a => a.TenantId == tenantId
                     && a.StartAt >= prevStartUtc
                     && a.StartAt < prevEndUtc
                     && a.Status != AppointmentStatus.Cancelled
                     && a.Status != AppointmentStatus.NoShow)
            .Select(a => a.Id)
            .ToListAsync(ct);

        var prevPayments = await _db.Payments
            .Where(p => p.TenantId == tenantId && prevApptIds.Contains(p.AppointmentId))
            .Select(p => new { p.Amount, p.Tip })
            .ToListAsync(ct);

        var prevRevenue = prevPayments.Sum(p => p.Amount.Amount + p.Tip.Amount);

        var revenueChangePct = ChangePct(totalRevenue, prevRevenue);
        var appointmentsChangePct = ChangePct(appointmentsCount, prevApptIds.Count);

        // ===== Ticket promedio =====
        var avgTicket = appointmentsCount > 0 ? totalRevenue / appointmentsCount : 0m;
        var prevAvgTicket = prevApptIds.Count > 0 ? prevRevenue / prevApptIds.Count : 0m;
        var avgTicketChangePct = ChangePct(avgTicket, prevAvgTicket);

        // ===== Tasa de no-show (período actual y previo) =====
        // Denominador: citas que llegaron a Confirmed o más (Confirmed +
        // InProgress + Completed + NoShow). Una cita Pending que se cancela
        // o expira no cuenta como "no-show".
        var (noShowRate, noShowCountCurrent) = await ComputeNoShowAsync(
            tenantId, startUtc, endUtc, ct);
        var (prevNoShowRate, _) = await ComputeNoShowAsync(
            tenantId, prevStartUtc, prevEndUtc, ct);
        var noShowChangePts = noShowRate - prevNoShowRate;

        // ===== Clientes nuevos =====
        var newCustomersCount = await _db.Customers
            .Where(c => c.TenantId == tenantId
                     && c.CreatedAt >= startUtc
                     && c.CreatedAt < endUtc)
            .CountAsync(ct);

        var prevNewCustomersCount = await _db.Customers
            .Where(c => c.TenantId == tenantId
                     && c.CreatedAt >= prevStartUtc
                     && c.CreatedAt < prevEndUtc)
            .CountAsync(ct);

        var newCustomersChangePct = ChangePct(newCustomersCount, prevNewCustomersCount);

        // ===== Enriquecer pagos con meta (serviceId, stylistId, startAt) =====
        var enriched = paymentsCurrent
            .Where(p => apptMetaById.ContainsKey(p.AppointmentId))
            .Select(p =>
            {
                var meta = apptMetaById[p.AppointmentId];
                return new EnrichedPayment(
                    p.AppointmentId,
                    meta.ServiceId,
                    meta.StylistId,
                    meta.StartAt,
                    p.Method,
                    p.Amount.Amount + p.Tip.Amount);
            })
            .ToList();

        // ===== Top servicios =====
        var topServicesAgg = enriched
            .GroupBy(x => x.ServiceId)
            .Select(g => new
            {
                ServiceId = g.Key,
                Revenue = g.Sum(x => x.Total),
                Cnt = g.Count(),
            })
            .OrderByDescending(x => x.Revenue)
            .Take(5)
            .ToList();

        var serviceIds = topServicesAgg.Select(s => s.ServiceId).ToList();
        var serviceNames = await _db.Services
            .Where(s => serviceIds.Contains(s.Id))
            .Select(s => new { s.Id, s.Name })
            .ToDictionaryAsync(s => s.Id, s => s.Name, ct);

        var topServices = topServicesAgg
            .Select(s => new TopServiceRow
            {
                ServiceId = s.ServiceId,
                ServiceName = serviceNames.GetValueOrDefault(s.ServiceId, "?"),
                AppointmentsCount = s.Cnt,
                Revenue = s.Revenue,
            })
            .ToList();

        // ===== Top estilistas con ocupación + no-shows =====
        // Total minutos abiertos del salón en el período (para denominador
        // de ocupación). Si no hay horario configurado, fallback razonable.
        var openMinutesPerStylist = await ComputeOpenMinutesAsync(
            tenantId, query.From, query.To, ct);

        // No-shows por estilista (sin filtro de status válido — son NoShows).
        var noShowsByStylist = await _db.Appointments
            .Where(a => a.TenantId == tenantId
                     && a.StartAt >= startUtc
                     && a.StartAt < endUtc
                     && a.Status == AppointmentStatus.NoShow)
            .GroupBy(a => a.StylistId)
            .Select(g => new { StylistId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.StylistId, x => x.Count, ct);

        var stylistAgg = enriched
            .GroupBy(x => x.StylistId)
            .Select(g => new
            {
                StylistId = g.Key,
                Revenue = g.Sum(x => x.Total),
                Cnt = g.Count(),
                // Sumamos minutos de servicio (no del pago — del appointment).
                MinutesBooked = g.Sum(x =>
                {
                    var meta = apptMetaById[x.AppointmentId];
                    return (meta.EndAt - meta.StartAt).TotalMinutes;
                }),
            })
            .OrderByDescending(x => x.Revenue)
            .Take(5)
            .ToList();

        var stylistIds = stylistAgg.Select(s => s.StylistId).ToList();
        var stylistInfo = await _db.Stylists
            .Where(s => stylistIds.Contains(s.Id))
            .Select(s => new { s.Id, s.FullName, s.Color })
            .ToDictionaryAsync(s => s.Id, ct);

        var topStylists = stylistAgg
            .Select(s => new TopStylistRow
            {
                StylistId = s.StylistId,
                StylistName = stylistInfo.GetValueOrDefault(s.StylistId)?.FullName ?? "?",
                StylistColor = stylistInfo.GetValueOrDefault(s.StylistId)?.Color,
                AppointmentsCount = s.Cnt,
                Revenue = s.Revenue,
                OccupancyPct = openMinutesPerStylist > 0
                    ? Math.Min(100.0, s.MinutesBooked / openMinutesPerStylist * 100.0)
                    : 0,
                NoShowCount = noShowsByStylist.GetValueOrDefault(s.StylistId, 0),
            })
            .ToList();

        // ===== Tendencia diaria de ingresos =====
        // Construye un punto por cada día del rango (aunque sea cero). El
        // frontend espera la serie completa para el eje X del area chart.
        var revenueByDay = enriched
            .GroupBy(x => DateOnly.FromDateTime((x.StartAt + ColombiaOffset).Date))
            .ToDictionary(g => g.Key, g => g.Sum(x => x.Total));

        var dailyRevenue = new List<DailyRevenuePoint>();
        for (var d = query.From; d <= query.To; d = d.AddDays(1))
        {
            dailyRevenue.Add(new DailyRevenuePoint
            {
                Date = d,
                Revenue = revenueByDay.GetValueOrDefault(d, 0m),
            });
        }

        // ===== Métodos de pago (para la dona) =====
        var methodAgg = enriched
            .GroupBy(x => x.Method)
            .Select(g => new
            {
                Method = g.Key,
                Revenue = g.Sum(x => x.Total),
            })
            .OrderByDescending(x => x.Revenue)
            .ToList();

        var paymentMethodBreakdown = methodAgg
            .Select(m => new PaymentMethodRow
            {
                Method = m.Method.ToString(),
                Label = LabelForMethod(m.Method),
                Revenue = m.Revenue,
                Percentage = totalRevenue > 0
                    ? (double)(m.Revenue / totalRevenue) * 100.0
                    : 0,
            })
            .ToList();

        // ===== Embudo Solicitadas → Atendidas =====
        // Solicitadas: todas las citas creadas en el período (regardless of status).
        // Confirmadas: pasaron a Confirmed/InProgress/Completed/NoShow.
        // Atendidas: Completed.
        // NoShow: el cliente no apareció.
        var statusBuckets = await _db.Appointments
            .Where(a => a.TenantId == tenantId
                     && a.StartAt >= startUtc
                     && a.StartAt < endUtc)
            .GroupBy(a => a.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        var byStatus = statusBuckets.ToDictionary(x => x.Status, x => x.Count);
        var requested = statusBuckets.Sum(x => x.Count);
        var confirmedPlus =
            byStatus.GetValueOrDefault(AppointmentStatus.Confirmed) +
            byStatus.GetValueOrDefault(AppointmentStatus.InProgress) +
            byStatus.GetValueOrDefault(AppointmentStatus.Completed) +
            byStatus.GetValueOrDefault(AppointmentStatus.NoShow);
        var attended = byStatus.GetValueOrDefault(AppointmentStatus.Completed);
        var noShowFunnel = byStatus.GetValueOrDefault(AppointmentStatus.NoShow);

        var funnel = new FunnelStats
        {
            Requested = requested,
            Confirmed = confirmedPlus,
            Attended = attended,
            NoShow = noShowFunnel,
        };

        // ===== Nuevos vs recurrentes en el período =====
        var customerIdsInPeriod = apptMeta.Select(a => a.CustomerId).Distinct().ToList();

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
        var newCustomerAppts = apptMeta.Count(a => !returningSet.Contains(a.CustomerId));
        var returningAppts = apptMeta.Count - newCustomerAppts;

        // ===== Insight + eyebrow =====
        var (insightEyebrow, insightText) = BuildInsight(
            query.From, query.To,
            revenueChangePct, noShowRate, noShowChangePts,
            topServices, topStylists);

        return Result<ReportsSummaryResponse>.Success(new ReportsSummaryResponse
        {
            From = query.From,
            To = query.To,
            TotalRevenue = totalRevenue,
            AppointmentsCount = appointmentsCount,
            AverageTicket = avgTicket,
            NoShowRate = noShowRate,
            NewCustomersCount = newCustomersCount,
            RevenueChangePct = revenueChangePct,
            AppointmentsChangePct = appointmentsChangePct,
            AverageTicketChangePct = avgTicketChangePct,
            NoShowChangePts = prevNoShowRate > 0 || noShowRate > 0 ? noShowChangePts : null,
            NewCustomersChangePct = newCustomersChangePct,
            TopServices = topServices,
            TopStylists = topStylists,
            DailyRevenue = dailyRevenue,
            PaymentMethodBreakdown = paymentMethodBreakdown,
            Funnel = funnel,
            NewCustomerAppointments = newCustomerAppts,
            ReturningCustomerAppointments = returningAppts,
            InsightEyebrow = insightEyebrow,
            InsightText = insightText,
        });
    }

    // ===== Helpers =====

    /// <summary>Convierte un rango [From, To] (inclusive) a UTC kinded.</summary>
    private static (DateTime startUtc, DateTime endUtc) ToUtcRange(DateOnly from, DateOnly to)
    {
        var startUtc = DateTime.SpecifyKind(
            from.ToDateTime(TimeOnly.MinValue) - ColombiaOffset, DateTimeKind.Utc);
        var endUtc = DateTime.SpecifyKind(
            to.AddDays(1).ToDateTime(TimeOnly.MinValue) - ColombiaOffset, DateTimeKind.Utc);
        return (startUtc, endUtc);
    }

    private static double? ChangePct(decimal current, decimal previous)
    {
        if (previous == 0m) return current == 0m ? 0.0 : (double?)null;
        return (double)((current - previous) / previous) * 100.0;
    }

    private static double? ChangePct(int current, int previous)
        => ChangePct((decimal)current, (decimal)previous);

    private async Task<(double rate, int count)> ComputeNoShowAsync(
        Guid tenantId, DateTime startUtc, DateTime endUtc, CancellationToken ct)
    {
        // Denominador: confirmadas + en progreso + completadas + no-show.
        // Citas Pending o Cancelled no entran al cálculo (nunca se confirmaron).
        var grouped = await _db.Appointments
            .Where(a => a.TenantId == tenantId
                     && a.StartAt >= startUtc
                     && a.StartAt < endUtc
                     && (a.Status == AppointmentStatus.Confirmed
                         || a.Status == AppointmentStatus.InProgress
                         || a.Status == AppointmentStatus.Completed
                         || a.Status == AppointmentStatus.NoShow))
            .GroupBy(a => a.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        var denom = grouped.Sum(x => x.Count);
        var noShows = grouped.FirstOrDefault(x => x.Status == AppointmentStatus.NoShow)?.Count ?? 0;
        var rate = denom > 0 ? (double)noShows / denom * 100.0 : 0.0;
        return (rate, noShows);
    }

    /// <summary>
    /// Devuelve los minutos totales abiertos del salón en el rango. Es el
    /// denominador POR ESTILISTA para ocupación — asumimos que cada
    /// estilista está disponible todo el horario salvo que el modelo
    /// tenga uno propio (no lo tiene aún).
    ///
    /// Si el tenant configuró SalonWeeklyHours, usamos esos rangos por día
    /// (lunes-domingo). Sino, fallback a 11h/día (8am–7pm) × días.
    /// </summary>
    private async Task<double> ComputeOpenMinutesAsync(
        Guid tenantId, DateOnly from, DateOnly to, CancellationToken ct)
    {
        var weekly = await _db.SalonWeeklyHours
            .Where(h => h.TenantId == tenantId)
            .ToListAsync(ct);

        var hoursPerWeekday = new Dictionary<int, double>();
        foreach (var h in weekly)
        {
            hoursPerWeekday[h.DayOfWeek] = h.ToHour - h.FromHour;
        }

        // Si no hay nada configurado, fallback razonable.
        if (hoursPerWeekday.Count == 0)
        {
            var defaultDays = 0;
            for (var d = from; d <= to; d = d.AddDays(1))
            {
                // Asume cerrado domingo.
                if (d.DayOfWeek != DayOfWeek.Sunday) defaultDays++;
            }
            return defaultDays * 11 * 60;
        }

        double total = 0;
        for (var d = from; d <= to; d = d.AddDays(1))
        {
            // .NET DayOfWeek: Sunday=0..Saturday=6 → nuestra convención Monday=0..Sunday=6
            var dotnet = (int)d.DayOfWeek;
            var dayOfWeek = (dotnet + 6) % 7;
            if (hoursPerWeekday.TryGetValue(dayOfWeek, out var hours))
            {
                total += hours * 60;
            }
        }
        return total;
    }

    private static string LabelForMethod(PaymentMethod m) => m switch
    {
        PaymentMethod.Cash => "Efectivo",
        PaymentMethod.Transfer => "Transferencia",
        PaymentMethod.Card => "Tarjeta",
        PaymentMethod.Other => "Otro",
        _ => m.ToString(),
    };

    /// <summary>
    /// Genera un texto en español para la card oscura "Lectura del…".
    /// Combina los hallazgos más significativos del período. Vuelve null si
    /// no hay nada interesante que decir (período sin actividad).
    /// </summary>
    private static (string eyebrow, string? text) BuildInsight(
        DateOnly from, DateOnly to,
        double? revenueChangePct, double noShowRate, double noShowChangePts,
        IReadOnlyList<TopServiceRow> topServices,
        IReadOnlyList<TopStylistRow> topStylists)
    {
        // Eyebrow según el largo del rango.
        var days = to.DayNumber - from.DayNumber + 1;
        var eyebrow = days switch
        {
            <= 1 => "Lectura del día",
            <= 7 => "Lectura de la semana",
            <= 31 => "Lectura del período",
            _ => "Lectura del período",
        };

        var parts = new List<string>();

        if (revenueChangePct.HasValue)
        {
            var pct = revenueChangePct.Value;
            if (pct >= 5)
                parts.Add($"Subiste {pct:F0}% en ingresos respecto al período anterior");
            else if (pct <= -5)
                parts.Add($"Bajaste {Math.Abs(pct):F0}% en ingresos respecto al período anterior");
        }

        if (noShowChangePts < -1 && noShowRate < 10)
        {
            parts.Add($"redujiste el no-show a {noShowRate:F1}%");
        }

        if (topServices.Count > 0)
        {
            parts.Add($"el servicio que más vendió fue {topServices[0].ServiceName}");
        }

        // Si hay un estilista con > 85% de ocupación, sugerir abrir cupos.
        var saturated = topStylists.FirstOrDefault(s => s.OccupancyPct >= 85);
        if (saturated is not null)
        {
            parts.Add($"considera abrir más cupos con {saturated.StylistName.Split(' ')[0]}");
        }

        if (parts.Count == 0) return (eyebrow, null);

        // Capitaliza la primera palabra y arma la oración.
        var first = parts[0];
        first = char.ToUpperInvariant(first[0]) + first[1..];
        var rest = string.Join("; ", parts.Skip(1));
        var text = parts.Count == 1
            ? first + "."
            : first + ". " + char.ToUpperInvariant(rest[0]) + rest[1..] + ".";

        return (eyebrow, text);
    }

    private readonly record struct EnrichedPayment(
        Guid AppointmentId,
        Guid ServiceId,
        Guid StylistId,
        DateTime StartAt,
        PaymentMethod Method,
        decimal Total);
}
