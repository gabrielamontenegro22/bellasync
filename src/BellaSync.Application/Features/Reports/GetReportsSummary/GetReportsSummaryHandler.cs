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
/// el dashboard /reportes (mockup v2) necesita en una sola call.
///
/// Reglas de negocio importantes:
///
///   - ANTICIPOS NO REEMBOLSABLES: cuando un cliente cancela una cita
///     después de pagar el anticipo, el salón se queda con esa plata.
///     Por eso TODO voucher en estado Validated cuenta como ingreso del
///     período, sin importar el status de la cita asociada (Cancelled,
///     NoShow, Completed, etc.).
///
///   - "Citas atendidas" = Completed + InProgress (citas que realmente
///     ocurrieron o están ocurriendo). Cancelled, NoShow, Pending y
///     Confirmed pendientes NO cuentan en este KPI.
///
///   - Ticket promedio se calcula sobre las atendidas únicamente
///     (revenue_atendidas / count_atendidas), para que represente
///     "cuánto típicamente cobramos por servicio realizado".
///
///   - Ingresos = Payment + Tip (de citas Completed/InProgress) +
///     vouchers Validated (de TODO el rango). Los forfeitures cuentan
///     como Ingresos pero NO como ticket promedio.
///
/// Estrategia técnica:
///   - Materializamos pagos y vouchers a memoria una vez — EF no traduce
///     la suma de Money VOs a SQL.
///   - Las agregaciones (top, daily, methods, occupancy) se hacen en
///     memoria sobre un set unificado de "RevenueItem" que combina
///     pagos y vouchers en una misma estructura.
///
/// Fechas: rango [From 00:00 CO, To+1día 00:00 CO). SpecifyKind(Utc).
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

        // ===== Período actual: revenue items unificados =====
        var current = await BuildRevenueItemsAsync(tenantId, startUtc, endUtc, ct);

        // ===== Período anterior: solo totales (sin desglose) =====
        var previous = await BuildRevenueItemsAsync(tenantId, prevStartUtc, prevEndUtc, ct);

        // ===== Citas atendidas (Completed + InProgress del período) =====
        var attendedCount = current.AllAppointments
            .Count(a => a.Status == AppointmentStatus.Completed
                     || a.Status == AppointmentStatus.InProgress);
        var prevAttendedCount = previous.AllAppointments
            .Count(a => a.Status == AppointmentStatus.Completed
                     || a.Status == AppointmentStatus.InProgress);

        // ===== Ingresos totales (incluyen forfeitures) =====
        var totalRevenue = current.Items.Sum(x => x.Amount);
        var prevRevenue = previous.Items.Sum(x => x.Amount);
        var revenueChangePct = ChangePct(totalRevenue, prevRevenue);
        var appointmentsChangePct = ChangePct(attendedCount, prevAttendedCount);

        // ===== Ticket promedio (solo de atendidas, sin forfeitures) =====
        var attendedRevenue = current.Items
            .Where(x => x.IsAttended)
            .Sum(x => x.Amount);
        var prevAttendedRevenue = previous.Items
            .Where(x => x.IsAttended)
            .Sum(x => x.Amount);
        var avgTicket = attendedCount > 0 ? attendedRevenue / attendedCount : 0m;
        var prevAvgTicket = prevAttendedCount > 0 ? prevAttendedRevenue / prevAttendedCount : 0m;
        var avgTicketChangePct = ChangePct(avgTicket, prevAvgTicket);

        // ===== Tasa de no-show =====
        var (noShowRate, _) = ComputeNoShowRate(current.AllAppointments);
        var (prevNoShowRate, _) = ComputeNoShowRate(previous.AllAppointments);
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

        // ===== Top servicios (incluye forfeitures) =====
        var topServicesAgg = current.Items
            .GroupBy(x => x.ServiceId)
            .Select(g => new
            {
                ServiceId = g.Key,
                Revenue = g.Sum(x => x.Amount),
                Cnt = g.Select(x => x.AppointmentId).Distinct().Count(),
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
        var openMinutesPerStylist = await ComputeOpenMinutesAsync(
            tenantId, query.From, query.To, ct);

        var noShowsByStylist = current.AllAppointments
            .Where(a => a.Status == AppointmentStatus.NoShow)
            .GroupBy(a => a.StylistId)
            .ToDictionary(g => g.Key, g => g.Count());

        // Para ocupación: cuenta minutos comprometidos (Completed +
        // InProgress + NoShow — el estilista estuvo bloqueado en ese
        // slot). Cancelled libera el slot, no cuenta.
        var minutesByStylist = current.AllAppointments
            .Where(a => a.Status == AppointmentStatus.Completed
                     || a.Status == AppointmentStatus.InProgress
                     || a.Status == AppointmentStatus.NoShow)
            .GroupBy(a => a.StylistId)
            .ToDictionary(g => g.Key, g => g.Sum(a => (a.EndAt - a.StartAt).TotalMinutes));

        var stylistAgg = current.Items
            .GroupBy(x => x.StylistId)
            .Select(g => new
            {
                StylistId = g.Key,
                Revenue = g.Sum(x => x.Amount),
                Cnt = g.Select(x => x.AppointmentId).Distinct().Count(),
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
                    ? Math.Min(100.0, minutesByStylist.GetValueOrDefault(s.StylistId, 0) / openMinutesPerStylist * 100.0)
                    : 0,
                NoShowCount = noShowsByStylist.GetValueOrDefault(s.StylistId, 0),
            })
            .ToList();

        // ===== Tendencia diaria de ingresos =====
        var revenueByDay = current.Items
            .GroupBy(x => DateOnly.FromDateTime((x.StartAt + ColombiaOffset).Date))
            .ToDictionary(g => g.Key, g => g.Sum(x => x.Amount));

        var dailyRevenue = new List<DailyRevenuePoint>();
        for (var d = query.From; d <= query.To; d = d.AddDays(1))
        {
            dailyRevenue.Add(new DailyRevenuePoint
            {
                Date = d,
                Revenue = revenueByDay.GetValueOrDefault(d, 0m),
            });
        }

        // ===== Métodos de pago (Method + Provider) =====
        var methodAgg = current.Items
            .GroupBy(x => new { x.Method, x.Provider })
            .Select(g => new
            {
                g.Key.Method,
                g.Key.Provider,
                Revenue = g.Sum(x => x.Amount),
            })
            .OrderByDescending(x => x.Revenue)
            .ToList();

        var paymentMethodBreakdown = methodAgg
            .Select(m => new PaymentMethodRow
            {
                Method = m.Method.ToString(),
                Provider = m.Provider,
                Label = !string.IsNullOrWhiteSpace(m.Provider)
                    ? m.Provider!
                    : LabelForMethod(m.Method),
                Revenue = m.Revenue,
                Percentage = totalRevenue > 0
                    ? (double)(m.Revenue / totalRevenue) * 100.0
                    : 0,
            })
            .ToList();

        // ===== Embudo Solicitadas → Atendidas =====
        var byStatus = current.AllAppointments
            .GroupBy(a => a.Status)
            .ToDictionary(g => g.Key, g => g.Count());

        var funnel = new FunnelStats
        {
            Requested = current.AllAppointments.Count,
            Confirmed =
                byStatus.GetValueOrDefault(AppointmentStatus.Confirmed) +
                byStatus.GetValueOrDefault(AppointmentStatus.InProgress) +
                byStatus.GetValueOrDefault(AppointmentStatus.Completed) +
                byStatus.GetValueOrDefault(AppointmentStatus.NoShow),
            Attended = byStatus.GetValueOrDefault(AppointmentStatus.Completed),
            NoShow = byStatus.GetValueOrDefault(AppointmentStatus.NoShow),
        };

        // ===== Nuevos vs recurrentes en el período (sobre atendidas) =====
        var attendedAppts = current.AllAppointments
            .Where(a => a.Status == AppointmentStatus.Completed
                     || a.Status == AppointmentStatus.InProgress)
            .ToList();
        var customerIdsInPeriod = attendedAppts.Select(a => a.CustomerId).Distinct().ToList();

        var customersWithPriorVisit = await _db.Appointments
            .Where(a => a.TenantId == tenantId
                     && customerIdsInPeriod.Contains(a.CustomerId)
                     && a.StartAt < startUtc
                     && a.Status == AppointmentStatus.Completed)
            .Select(a => a.CustomerId)
            .Distinct()
            .ToListAsync(ct);

        var returningSet = customersWithPriorVisit.ToHashSet();
        var newCustomerAppts = attendedAppts.Count(a => !returningSet.Contains(a.CustomerId));
        var returningAppts = attendedAppts.Count - newCustomerAppts;

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
            AppointmentsCount = attendedCount,
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

    /// <summary>
    /// Trae TODAS las citas del período (cualquier status) más todos los
    /// Payments y Vouchers Validated asociados, y los unifica en una lista
    /// de RevenueItems donde cada item representa "plata recibida" con su
    /// método/provider/source.
    ///
    /// Vouchers Validated cuentan SIEMPRE (anticipos no reembolsables):
    /// si una cita con anticipo se cancela, el salón se queda con el
    /// dinero. Payments cuentan solo para citas Completed/InProgress (el
    /// caso normal donde se cobra al terminar el servicio).
    /// </summary>
    private async Task<RevenueAggregation> BuildRevenueItemsAsync(
        Guid tenantId, DateTime startUtc, DateTime endUtc, CancellationToken ct)
    {
        var allAppts = await _db.Appointments
            .Where(a => a.TenantId == tenantId
                     && a.StartAt >= startUtc
                     && a.StartAt < endUtc)
            .Select(a => new ApptInfo(
                a.Id, a.CustomerId, a.ServiceId, a.StylistId,
                a.StartAt, a.EndAt, a.Status))
            .ToListAsync(ct);

        var apptById = allAppts.ToDictionary(a => a.Id);
        var apptIds = allAppts.Select(a => a.Id).ToList();

        // Payments: solo de citas Completed/InProgress. Las Cancelled/NoShow
        // no deberían tener un Payment normal (el flujo de Cobrar requiere
        // que la cita esté en curso o terminada).
        var attendedIds = allAppts
            .Where(a => a.Status == AppointmentStatus.Completed
                     || a.Status == AppointmentStatus.InProgress)
            .Select(a => a.Id)
            .ToList();

        var payments = await _db.Payments
            .Where(p => p.TenantId == tenantId && attendedIds.Contains(p.AppointmentId))
            .Select(p => new { p.AppointmentId, p.Method, p.Provider, p.Amount, p.Tip })
            .ToListAsync(ct);

        // Vouchers Validated: de TODAS las citas del período. Anticipos
        // no reembolsables → cuentan independiente del status final.
        //
        // EXCLUIR vouchers internos: representan aplicación de crédito viejo,
        // la plata ya se contabilizó en el período en que se cobró el voucher
        // ORIGINAL externo (días/semanas atrás). Contarlos otra vez infla
        // KPIs de ingresos / ticket promedio / top services del período actual.
        var vouchers = await _db.PaymentVouchers
            .Where(v => v.TenantId == tenantId
                     && apptIds.Contains(v.AppointmentId)
                     && v.Status == PaymentVoucherStatus.Validated
                     && !v.IsInternalCredit)
            .Select(v => new { v.AppointmentId, v.ReportedAmount, v.Bank })
            .ToListAsync(ct);

        var items = new List<RevenueItem>(payments.Count + vouchers.Count);

        foreach (var p in payments)
        {
            if (!apptById.TryGetValue(p.AppointmentId, out var info)) continue;
            items.Add(new RevenueItem(
                AppointmentId: p.AppointmentId,
                ServiceId: info.ServiceId,
                StylistId: info.StylistId,
                StartAt: info.StartAt,
                Status: info.Status,
                Method: p.Method,
                Provider: p.Provider,
                Amount: p.Amount.Amount + p.Tip.Amount,
                Source: RevenueSource.Payment));
        }

        foreach (var v in vouchers)
        {
            if (!apptById.TryGetValue(v.AppointmentId, out var info)) continue;
            items.Add(new RevenueItem(
                AppointmentId: v.AppointmentId,
                ServiceId: info.ServiceId,
                StylistId: info.StylistId,
                StartAt: info.StartAt,
                Status: info.Status,
                // Anticipos por transferencia es el caso normal.
                Method: PaymentMethod.Transfer,
                Provider: string.IsNullOrWhiteSpace(v.Bank) ? null : v.Bank,
                Amount: v.ReportedAmount.Amount,
                Source: RevenueSource.Deposit));
        }

        return new RevenueAggregation(allAppts, items);
    }

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
        // Ambos cero: sin variación, mostramos 0%.
        if (previous == 0m && current == 0m) return 0.0;
        // Sólo el anterior cero (período anterior arrancó vacío): no podemos
        // calcular % de cambio porque dividiríamos por cero. Devolvemos un
        // sentinel grande (+1000%) que el frontend interpreta como "salto
        // enorme" en vez de null (que pintaría "—" y daría sensación de
        // bug). No usamos double.PositiveInfinity porque no es JSON-safe.
        if (previous == 0m) return 1000.0;
        return (double)((current - previous) / previous) * 100.0;
    }

    private static double? ChangePct(int current, int previous)
        => ChangePct((decimal)current, (decimal)previous);

    /// <summary>
    /// Tasa de no-show: NoShow / (Confirmed + InProgress + Completed + NoShow).
    /// Citas Pending o Cancelled no entran al cálculo (nunca se confirmaron).
    /// </summary>
    private static (double rate, int count) ComputeNoShowRate(IReadOnlyList<ApptInfo> appts)
    {
        var denom = appts.Count(a =>
            a.Status == AppointmentStatus.Confirmed
            || a.Status == AppointmentStatus.InProgress
            || a.Status == AppointmentStatus.Completed
            || a.Status == AppointmentStatus.NoShow);
        var noShows = appts.Count(a => a.Status == AppointmentStatus.NoShow);
        var rate = denom > 0 ? (double)noShows / denom * 100.0 : 0.0;
        return (rate, noShows);
    }

    /// <summary>
    /// Minutos abiertos del salón en el rango. Usa SalonWeeklyHours si está
    /// configurado, sino fallback de 11h × días no-domingo. No considera
    /// vacaciones por estilista (no tenemos esa entidad aún).
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

        if (hoursPerWeekday.Count == 0)
        {
            var defaultDays = 0;
            for (var d = from; d <= to; d = d.AddDays(1))
            {
                if (d.DayOfWeek != DayOfWeek.Sunday) defaultDays++;
            }
            return defaultDays * 11 * 60;
        }

        double total = 0;
        for (var d = from; d <= to; d = d.AddDays(1))
        {
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

    private static (string eyebrow, string? text) BuildInsight(
        DateOnly from, DateOnly to,
        double? revenueChangePct, double noShowRate, double noShowChangePts,
        IReadOnlyList<TopServiceRow> topServices,
        IReadOnlyList<TopStylistRow> topStylists)
    {
        var days = to.DayNumber - from.DayNumber + 1;
        var eyebrow = days switch
        {
            <= 1 => "Lectura del día",
            <= 7 => "Lectura de la semana",
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

        var saturated = topStylists.FirstOrDefault(s => s.OccupancyPct >= 85);
        if (saturated is not null)
        {
            parts.Add($"considera abrir más cupos con {saturated.StylistName.Split(' ')[0]}");
        }

        if (parts.Count == 0) return (eyebrow, null);

        var first = parts[0];
        first = char.ToUpperInvariant(first[0]) + first[1..];
        var rest = string.Join("; ", parts.Skip(1));
        var text = parts.Count == 1
            ? first + "."
            : first + ". " + char.ToUpperInvariant(rest[0]) + rest[1..] + ".";

        return (eyebrow, text);
    }

    // ===== Tipos internos =====

    private enum RevenueSource { Payment, Deposit }

    private readonly record struct ApptInfo(
        Guid Id,
        Guid CustomerId,
        Guid ServiceId,
        Guid StylistId,
        DateTime StartAt,
        DateTime EndAt,
        AppointmentStatus Status);

    private readonly record struct RevenueItem(
        Guid AppointmentId,
        Guid ServiceId,
        Guid StylistId,
        DateTime StartAt,
        AppointmentStatus Status,
        PaymentMethod Method,
        string? Provider,
        decimal Amount,
        RevenueSource Source)
    {
        /// <summary>True si la cita asociada se atendió (Completed/InProgress).</summary>
        public bool IsAttended =>
            Status == AppointmentStatus.Completed
            || Status == AppointmentStatus.InProgress;
    }

    private sealed record RevenueAggregation(
        IReadOnlyList<ApptInfo> AllAppointments,
        IReadOnlyList<RevenueItem> Items);
}
