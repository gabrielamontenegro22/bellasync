namespace BellaSync.Application.Features.Dashboard.Dtos;

/// <summary>
/// Snapshot que arma el dashboard de bienvenida del SalonAdmin.
/// Una sola call con todo lo que necesita la home — evita N queries
/// del frontend al renderizar las cards.
///
/// También usado por el sidebar para los badges (pendingVouchersCount).
/// </summary>
public sealed class DashboardSummaryResponse
{
    // Hoy
    public DateOnly Today { get; init; }
    public int TodayAppointmentsCount { get; init; }
    public int TodayCompletedCount { get; init; }
    public int TodayPendingCount { get; init; }
    public decimal TodayRevenue { get; init; }

    // Próxima cita (la siguiente del día, hora actual hacia adelante)
    public NextAppointmentDto? NextAppointment { get; init; }

    // Esta semana (lunes a domingo)
    public int WeekAppointmentsCount { get; init; }
    public decimal WeekRevenue { get; init; }

    // Pendientes para badges del sidebar y avisos
    public int PendingVouchersCount { get; init; }

    /// <summary>True si la caja del día NO está cerrada todavía.</summary>
    public bool CashClosingPending { get; init; }
}

public sealed class NextAppointmentDto
{
    public Guid Id { get; init; }
    public string CustomerName { get; init; } = string.Empty;
    public string ServiceName { get; init; } = string.Empty;
    public string StylistName { get; init; } = string.Empty;
    public string? StylistColor { get; init; }
    public DateTime StartAt { get; init; }
    public DateTime EndAt { get; init; }
    public string Status { get; init; } = string.Empty;
}
