namespace BellaSync.Domain.Entities;

/// <summary>
/// Canal por el cual se originó la cita. Útil para métricas y para mostrar
/// el origen en la agenda (ej. una cita Reception vs PublicPortal puede
/// mostrarse con badge distinto).
/// </summary>
public enum AppointmentChannel
{
    /// <summary>Agendada por recepción desde el panel interno.</summary>
    Reception = 0,

    /// <summary>Agendada por el cliente desde el portal público.</summary>
    PublicPortal = 1,
}
