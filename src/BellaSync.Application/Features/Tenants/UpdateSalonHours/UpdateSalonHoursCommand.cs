using BellaSync.Application.Common.Handlers;
using BellaSync.Application.Features.Tenants.Dtos;

namespace BellaSync.Application.Features.Tenants.UpdateSalonHours;

/// <summary>
/// Replace-all del horario. El handler:
///   1. Actualiza los flags del Tenant (almuerzo, festivos).
///   2. Borra todas las SalonWeeklyHours del tenant y recrea las
///      filas según el Days dict (solo días con valor distinto de null).
///   3. Borra todas las SalonClosedDates del tenant y recrea según
///      la lista ClosedDates.
/// Es replace-all en lugar de delta para simplicidad y matchear el
/// modelo mental del form (la admin ve el horario completo cuando
/// guarda).
/// </summary>
public sealed record UpdateSalonHoursCommand(
    Dictionary<int, DayRange?> Days,
    bool LunchBreakEnabled,
    int LunchBreakFromHour,
    int LunchBreakToHour,
    bool IsHolidaysClosed,
    List<string> ClosedDates
) : ICommand<SalonHoursResponse>;
