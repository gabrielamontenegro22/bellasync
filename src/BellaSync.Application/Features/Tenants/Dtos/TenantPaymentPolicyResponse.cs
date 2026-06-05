namespace BellaSync.Application.Features.Tenants.Dtos;

/// <summary>
/// Política de pagos vigente del salón. Espejo de las 3 columnas que
/// agregamos a la tabla tenants.
/// </summary>
public class TenantPaymentPolicyResponse
{
    /// <summary>Horas máximas que el cupo queda reservado tras agendar.</summary>
    public int HoldDurationHours { get; set; }

    /// <summary>Minutos antes de la cita en que el hold deja de aplicar.</summary>
    public int HoldMinBeforeAppointmentMinutes { get; set; }

    /// <summary>Anticipación mínima para poder agendar.</summary>
    public int MinAdvanceMinutes { get; set; }

    /// <summary>
    /// Horas antes de la cita dentro de las cuales una cancelación
    /// devuelve el anticipo. Pasada esa ventana, el anticipo se
    /// pierde (Forfeited) salvo override de admin.
    /// Default: 2h. Rango válido: 0–168h (una semana).
    /// </summary>
    public int CancellationWindowHours { get; set; }
}
