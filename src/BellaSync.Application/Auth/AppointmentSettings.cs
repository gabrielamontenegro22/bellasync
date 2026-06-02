namespace BellaSync.Application.Auth;

/// <summary>
/// Sección "Appointments" en appsettings.json — política de hold y agendamiento.
///
/// (Vive en namespace Application.Auth por consistencia con JwtSettings;
/// más adelante puede migrar a Application.Settings cuando haya más.)
/// </summary>
public class AppointmentSettings
{
    public const string SectionName = "Appointments";

    /// <summary>
    /// Cuánto tiempo desde la creación se mantiene el cupo reservado sin
    /// validación del anticipo. Default: 3 horas.
    /// </summary>
    public int HoldDurationHours { get; set; } = 3;

    /// <summary>
    /// Cuánto tiempo antes de la cita se libera el hold sí o sí (aunque
    /// el HoldDuration aún no expire). Default: 30 minutos.
    /// El hold real es min(creation + HoldDuration, StartAt - HoldMinBefore).
    /// </summary>
    public int HoldMinBeforeAppointmentMinutes { get; set; } = 30;

    /// <summary>
    /// Mínimo de minutos de anticipación para agendar una cita
    /// (no se puede agendar a las 14:00 si ya son las 13:45). Default: 30.
    /// </summary>
    public int MinAdvanceMinutes { get; set; } = 30;
}
