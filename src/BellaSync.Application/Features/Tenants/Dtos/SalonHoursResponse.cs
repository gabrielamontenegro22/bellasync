namespace BellaSync.Application.Features.Tenants.Dtos;

/// <summary>
/// Horario completo del salón en un solo DTO — días, almuerzo,
/// festivos, cierres puntuales. Lo que el form de Configuración →
/// Horario consume y manda de vuelta.
///
/// Days: dict 0-6 (Lun..Dom) → {from, to} | null. Null = cerrado.
/// </summary>
public class SalonHoursResponse
{
    public Dictionary<int, DayRange?> Days { get; set; } = new();

    public bool LunchBreakEnabled { get; set; }
    public int LunchBreakFromHour { get; set; }
    public int LunchBreakToHour { get; set; }

    public bool IsHolidaysClosed { get; set; }

    /// <summary>Lista de fechas YYYY-MM-DD ordenadas cronológicamente.</summary>
    public List<string> ClosedDates { get; set; } = new();
}

public class DayRange
{
    public int FromHour { get; set; }
    public int ToHour { get; set; }
}
