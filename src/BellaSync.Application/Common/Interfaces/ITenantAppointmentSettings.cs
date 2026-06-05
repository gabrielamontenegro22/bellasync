namespace BellaSync.Application.Common.Interfaces;

/// <summary>
/// Resuelve la política de pagos vigente para el tenant del request actual.
///
/// Antes esto era un IOptions&lt;AppointmentSettings&gt; con valores globales
/// para todo el SaaS. Pero cada salón quiere ajustarlos (un spa relajado
/// quizá da 24h para pagar, una peluquería express solo 1h), entonces
/// migramos a leer las columnas del Tenant.
///
/// La implementación lee el TenantId del ICurrentTenantService y trae las
/// columnas hold_duration_hours / hold_min_before_appointment_minutes /
/// min_advance_minutes del tenant. Cacheable por scope porque los valores
/// no cambian dentro de un mismo request.
/// </summary>
public interface ITenantAppointmentSettings
{
    /// <summary>Horas máximas que un cupo queda reservado. Default 3.</summary>
    Task<int> GetHoldDurationHoursAsync(CancellationToken ct);

    /// <summary>Minutos antes de la cita en que el hold deja de aplicar. Default 30.</summary>
    Task<int> GetHoldMinBeforeAppointmentMinutesAsync(CancellationToken ct);

    /// <summary>Minutos mínimos de anticipación para agendar. Default 30.</summary>
    Task<int> GetMinAdvanceMinutesAsync(CancellationToken ct);

    /// <summary>
    /// Horas antes de la cita en las que cancelar todavía da derecho a
    /// devolución de anticipo. Default 2.
    /// </summary>
    Task<int> GetCancellationWindowHoursAsync(CancellationToken ct);
}
