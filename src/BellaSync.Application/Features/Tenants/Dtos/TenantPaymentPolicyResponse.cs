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
}
