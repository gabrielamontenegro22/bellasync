namespace BellaSync.Application.Features.Appointments.Dtos;

/// <summary>
/// Respuesta al cliente del portal público después de agendar una cita.
/// NO devuelve la cita completa — solo lo necesario para mostrar la
/// página de éxito (resumen + datos de transferencia si requiere anticipo).
/// </summary>
public class PublicBookingResponse
{
    public Guid AppointmentId { get; set; }
    public DateTime StartAt { get; set; }
    public string ServiceName { get; set; } = string.Empty;
    public string StylistName { get; set; } = string.Empty;
    public decimal PriceSnapshot { get; set; }

    /// <summary>"Pending" si requiere anticipo, "Confirmed" si no.</summary>
    public string Status { get; set; } = string.Empty;

    public bool RequiresDeposit { get; set; }
    public decimal DepositAmount { get; set; }

    /// <summary>
    /// Cuándo expira el cupo si no se valida el anticipo. Null si no aplica.
    /// El frontend muestra "Reservado hasta {hora}" para urgencia.
    /// </summary>
    public DateTime? HoldExpiresAt { get; set; }
}
