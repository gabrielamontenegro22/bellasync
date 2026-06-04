namespace BellaSync.Application.Features.Reports.Dtos;

/// <summary>
/// Snapshot agregado para un período. La idea es que el frontend pueda
/// armar todo el dashboard de /reportes con UNA sola llamada — un endpoint
/// "summary" en vez de N endpoints separados. Mantiene el contrato del
/// frontend simple y permite agregar más cards en el futuro sin más
/// roundtrips.
///
/// Todas las cifras monetarias en COP (decimales pero el front las muestra
/// sin decimales). Conteos son enteros.
/// </summary>
public sealed class ReportsSummaryResponse
{
    /// <summary>Fecha "desde" usada para el cálculo (echo del input).</summary>
    public DateOnly From { get; init; }
    /// <summary>Fecha "hasta" inclusive.</summary>
    public DateOnly To { get; init; }

    // ===== KPIs principales (cards arriba) =====

    /// <summary>
    /// Total recaudado en el período (suma de Payment.Total de citas
    /// dentro del rango, contando solo Confirmed/InProgress/Completed —
    /// las Cancelled/NoShow no aportan).
    /// </summary>
    public decimal TotalRevenue { get; init; }

    /// <summary>
    /// Cantidad de citas que NO fueron canceladas ni no-show en el
    /// período. "Citas que realmente sucedieron o están agendadas."
    /// </summary>
    public int AppointmentsCount { get; init; }

    /// <summary>
    /// Ticket promedio = TotalRevenue / AppointmentsCount. 0 si no hay citas.
    /// </summary>
    public decimal AverageTicket { get; init; }

    /// <summary>Clientes creados en el período.</summary>
    public int NewCustomersCount { get; init; }

    // ===== Comparativa con período anterior =====

    /// <summary>
    /// Cambio % en revenue respecto al período inmediatamente anterior
    /// del mismo largo. Positivo = creció. null si el período anterior
    /// fue 0 (sería ÷0).
    /// </summary>
    public double? RevenueChangePct { get; init; }

    // ===== Top servicios =====

    public IReadOnlyList<TopServiceRow> TopServices { get; init; } = new List<TopServiceRow>();

    // ===== Top estilistas =====

    public IReadOnlyList<TopStylistRow> TopStylists { get; init; } = new List<TopStylistRow>();

    // ===== Tendencia semanal de ingresos =====

    /// <summary>
    /// Ingresos agrupados por semana ISO (lunes-domingo) — últimas 8
    /// semanas hasta hoy. Si el período custom es más chico, se
    /// devuelven menos. Para sparkline tipo bar chart.
    /// </summary>
    public IReadOnlyList<WeeklyRevenuePoint> WeeklyRevenue { get; init; } = new List<WeeklyRevenuePoint>();

    // ===== Nuevos vs recurrentes =====

    /// <summary>Citas del período cuyo customer es nuevo (1ra visita).</summary>
    public int NewCustomerAppointments { get; init; }

    /// <summary>Citas del período cuyo customer ya había venido antes.</summary>
    public int ReturningCustomerAppointments { get; init; }
}

public sealed class TopServiceRow
{
    public Guid ServiceId { get; init; }
    public string ServiceName { get; init; } = string.Empty;
    public int AppointmentsCount { get; init; }
    public decimal Revenue { get; init; }
}

public sealed class TopStylistRow
{
    public Guid StylistId { get; init; }
    public string StylistName { get; init; } = string.Empty;
    public string? StylistColor { get; init; }
    public int AppointmentsCount { get; init; }
    public decimal Revenue { get; init; }
}

public sealed class WeeklyRevenuePoint
{
    /// <summary>Lunes de la semana ISO.</summary>
    public DateOnly WeekStart { get; init; }
    public decimal Revenue { get; init; }
}
