namespace BellaSync.Application.Features.Appointments.Dtos;

/// <summary>
/// Vista agregada de la agenda de un día: métricas + lista de citas.
/// </summary>
public class AgendaResponse
{
    /// <summary>Fecha de la agenda (YYYY-MM-DD).</summary>
    public DateOnly Date { get; set; }

    public AgendaMetrics Metrics { get; set; } = new();

    public IReadOnlyList<AppointmentResponse> Appointments { get; set; } = Array.Empty<AppointmentResponse>();
}

public class AgendaMetrics
{
    /// <summary>Total citas en el día (excluyendo canceladas).</summary>
    public int Total { get; set; }

    /// <summary>Citas Pending con DepositStatus AwaitingPayment (esperan validación).</summary>
    public int PendingValidation { get; set; }

    /// <summary>Citas Confirmed (incluye InProgress).</summary>
    public int Confirmed { get; set; }

    /// <summary>Citas NoShow del día.</summary>
    public int NoShow { get; set; }
}
