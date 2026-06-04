namespace BellaSync.Application.Features.Reports.Dtos;

/// <summary>
/// Snapshot agregado para un período. La idea es que el frontend pueda
/// armar TODO el dashboard de /reportes con UNA sola llamada — un endpoint
/// "summary" en vez de N endpoints separados. Mantiene el contrato del
/// frontend simple y permite agregar más cards en el futuro sin más
/// roundtrips.
///
/// v2 (matching mockup): agrega tasa de no-show, breakdown por método
/// de pago (dona), embudo Solicitadas→Atendidas, daily revenue
/// (30 puntos), ocupación + noShowCount por estilista, y un insight
/// dinámico generado en backend.
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

    // ===== KPIs principales (5 cards arriba) =====

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

    /// <summary>
    /// Tasa de no-show del período (0–100). Calculada sobre el total de
    /// citas que llegaron a status Confirmed o superior (Confirmadas +
    /// InProgress + Completed + NoShow), no sobre Solicitadas — porque
    /// una cita que el cliente nunca confirmó no es realmente "no-show".
    /// La UI la muestra con lógica invertida: menos % = mejor (verde).
    /// </summary>
    public double NoShowRate { get; init; }

    /// <summary>Clientes creados en el período.</summary>
    public int NewCustomersCount { get; init; }

    // ===== Comparativa con período anterior =====

    /// <summary>
    /// Cambio % en revenue respecto al período inmediatamente anterior
    /// del mismo largo. Positivo = creció. null si el período anterior
    /// fue 0 (sería ÷0).
    /// </summary>
    public double? RevenueChangePct { get; init; }

    /// <summary>Mismo cálculo para citas (% vs período anterior).</summary>
    public double? AppointmentsChangePct { get; init; }

    /// <summary>Mismo cálculo para ticket promedio.</summary>
    public double? AverageTicketChangePct { get; init; }

    /// <summary>
    /// Cambio absoluto en puntos de la tasa de no-show. Negativo = mejoró.
    /// </summary>
    public double? NoShowChangePts { get; init; }

    /// <summary>Mismo cálculo para clientes nuevos.</summary>
    public double? NewCustomersChangePct { get; init; }

    // ===== Top servicios =====

    public IReadOnlyList<TopServiceRow> TopServices { get; init; } = new List<TopServiceRow>();

    // ===== Top estilistas (con ocupación + no-shows) =====

    public IReadOnlyList<TopStylistRow> TopStylists { get; init; } = new List<TopStylistRow>();

    // ===== Tendencia diaria de ingresos (para el area chart) =====

    /// <summary>
    /// Ingresos agrupados por DÍA — un punto por cada día del rango
    /// (hasta 30 días para no saturar). Si el rango es menor, se
    /// devuelven menos puntos. Reemplaza al WeeklyRevenue de v1.
    /// </summary>
    public IReadOnlyList<DailyRevenuePoint> DailyRevenue { get; init; } = new List<DailyRevenuePoint>();

    // ===== Métodos de pago (dona) =====

    /// <summary>
    /// Breakdown de ingresos por método (Cash/Transfer/Card/Other).
    /// Ordenado de mayor a menor revenue. Cuando hay Transfer con
    /// múltiples bancos, se agregan en una sola fila "Transferencia"
    /// para mantener la dona legible — el detalle por banco vive en
    /// /caja.
    /// </summary>
    public IReadOnlyList<PaymentMethodRow> PaymentMethodBreakdown { get; init; } = new List<PaymentMethodRow>();

    // ===== Embudo Solicitadas → Atendidas =====

    /// <summary>
    /// Conteos del embudo de conversión del período:
    ///   Solicitadas: TODAS las citas creadas en período (todos los status)
    ///   Confirmadas: las que llegaron a Confirmed/InProgress/Completed/NoShow
    ///   Atendidas:   Completed (la cita terminó OK)
    ///   NoShow:      el cliente no apareció (status=NoShow)
    /// </summary>
    public FunnelStats Funnel { get; init; } = new();

    // ===== Nuevos vs recurrentes =====

    /// <summary>Citas del período cuyo customer es nuevo (1ra visita).</summary>
    public int NewCustomerAppointments { get; init; }

    /// <summary>Citas del período cuyo customer ya había venido antes.</summary>
    public int ReturningCustomerAppointments { get; init; }

    // ===== Insight dinámico (card oscura "Lectura del mes") =====

    /// <summary>
    /// Texto en español de 1-2 oraciones generado a partir de los datos:
    /// destaca el cambio % en revenue, mejora en no-show, top servicio,
    /// etc. El frontend lo renderiza tal cual en una card oscura. Si no
    /// hay suficientes datos para generar algo útil, queda en null y la
    /// UI esconde la card.
    /// </summary>
    public string? InsightText { get; init; }

    /// <summary>
    /// Eyebrow corto para el insight ("Lectura del mes", "Lectura del
    /// período", etc.) — se computa según el rango.
    /// </summary>
    public string InsightEyebrow { get; init; } = "Lectura del período";
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

    /// <summary>
    /// Ocupación del estilista (0–100): proporción del horario del salón
    /// que tuvo cita. Calculado como sum(durationMinutes de citas válidas)
    /// / total_open_minutes_del_salón_en_el_período × 100. No considera
    /// vacaciones o licencias del estilista (no tenemos ese modelo aún).
    /// </summary>
    public double OccupancyPct { get; init; }

    /// <summary>
    /// Cantidad de no-shows que tuvo este estilista en el período.
    /// La UI lo muestra discretamente — un estilista con muchos no-shows
    /// puede ser señal de problemas operativos.
    /// </summary>
    public int NoShowCount { get; init; }
}

public sealed class DailyRevenuePoint
{
    public DateOnly Date { get; init; }
    public decimal Revenue { get; init; }
}

public sealed class PaymentMethodRow
{
    /// <summary>"Cash" | "Transfer" | "Card" | "Other" — string del enum.</summary>
    public string Method { get; init; } = string.Empty;
    /// <summary>Label en español para mostrar ("Efectivo", "Transferencia"…).</summary>
    public string Label { get; init; } = string.Empty;
    public decimal Revenue { get; init; }
    public double Percentage { get; init; }
}

public sealed class FunnelStats
{
    public int Requested { get; init; }
    public int Confirmed { get; init; }
    public int Attended { get; init; }
    public int NoShow { get; init; }
}
