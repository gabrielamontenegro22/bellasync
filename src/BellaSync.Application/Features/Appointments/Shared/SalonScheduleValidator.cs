using BellaSync.Application.Common.Errors;
using BellaSync.Application.Common.Interfaces;
using BellaSync.Application.Common.Results;
using BellaSync.Application.Common.Services;
using Microsoft.EntityFrameworkCore;

namespace BellaSync.Application.Features.Appointments.Shared;

/// <summary>
/// Valida que una franja propuesta (startUtc, endUtc) cae dentro del
/// horario que el salón configuró: día abierto, dentro del rango,
/// no en la franja de almuerzo, no en un cierre puntual, no en
/// festivo si la opción está activa.
///
/// Convertimos a hora local Colombia (UTC-5) para todos los chequeos
/// porque la admin configura horarios en hora local.
///
/// Las citas que sobrepasan medianoche (ej. termina 02:00 del día
/// siguiente) no son contempladas en v1 — todos los servicios del
/// salón terminan en el mismo día. Si se necesita en el futuro,
/// extender este validator.
/// </summary>
public sealed class SalonScheduleValidator
{
    private static readonly TimeSpan ColombiaOffset = TimeSpan.FromHours(-5);

    private readonly IApplicationDbContext _db;

    public SalonScheduleValidator(IApplicationDbContext db)
    {
        _db = db;
    }

    /// <summary>
    /// Chequea la franja contra el horario de un salón. Devuelve
    /// Result.Success() si pasa, o un ApplicationError de Validación
    /// con un mensaje accionable si no.
    ///
    /// Si bypass=true (SalonAdmin lo activó explícitamente para una
    /// cita imprevista / walk-in), salta toda la validación.
    ///
    /// tenantId se pasa explícitamente para que también sirva en el
    /// flujo anónimo del portal público (que resuelve tenant por slug
    /// y no tiene ICurrentTenantService disponible).
    /// </summary>
    public async Task<Result<bool>> ValidateAsync(
        Guid tenantId,
        DateTime startUtc,
        DateTime endUtc,
        bool bypass,
        CancellationToken ct)
    {
        if (bypass) return Result<bool>.Success(true);

        // 0) Opt-in. Si el tenant no configuró NINGÚN horario semanal
        //    todavía, consideramos que la regla no está activa y dejamos
        //    pasar. Evita lockouts en salones existentes que aún no
        //    entraron a /configuracion/horario, y también en tests.
        var hasAnyHours = await _db.SalonWeeklyHours
            .IgnoreQueryFilters()
            .AnyAsync(h => h.TenantId == tenantId, ct);
        if (!hasAnyHours) return Result<bool>.Success(true);

        // 1) Convertir a hora Colombia para razonar con día/hora local.
        var startLocal = startUtc.Add(ColombiaOffset);
        var endLocal = endUtc.Add(ColombiaOffset);
        var date = DateOnly.FromDateTime(startLocal);

        // 2) Cierres puntuales en esta fecha. IgnoreQueryFilters por si
        // venimos del flujo público anónimo (sin tenant resuelto en
        // el filtro global). Filtramos por tenantId explícito.
        var closed = await _db.SalonClosedDates
            .IgnoreQueryFilters()
            .AnyAsync(c => c.TenantId == tenantId && c.ClosedDate == date, ct);
        if (closed)
        {
            return ApplicationError.Validation(
                "schedule.closed_date",
                $"El salón tiene un cierre puntual programado para el {date:dd/MM/yyyy}.");
        }

        // 3) Festivos nacionales (si la opción está activa).
        var tenant = await _db.Tenants
            .IgnoreQueryFilters()
            .Where(t => t.Id == tenantId)
            .Select(t => new
            {
                t.IsHolidaysClosed,
                t.LunchBreakEnabled,
                t.LunchBreakFromHour,
                t.LunchBreakToHour,
            })
            .FirstOrDefaultAsync(ct);
        if (tenant is null)
        {
            // Defensivo: si no encontramos el tenant, dejamos pasar
            // (no es un problema de horario sino de auth/lookup).
            return Result<bool>.Success(true);
        }

        if (tenant.IsHolidaysClosed && ColombiaHolidayCalendar.IsHoliday(date))
        {
            return ApplicationError.Validation(
                "schedule.holiday",
                $"El {date:dd/MM/yyyy} es festivo nacional y el salón está configurado como cerrado en festivos.");
        }

        // 4) Día de la semana abierto y dentro del rango de horas.
        //    Mapeamos DayOfWeek de .NET (Sunday=0..Saturday=6) a nuestra
        //    convención (Monday=0..Sunday=6).
        var dotnetDow = (int)startLocal.DayOfWeek;
        var dayOfWeek = (dotnetDow + 6) % 7;  // Sunday→6, Monday→0, …

        var dayHours = await _db.SalonWeeklyHours
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(h => h.TenantId == tenantId && h.DayOfWeek == dayOfWeek, ct);

        if (dayHours is null)
        {
            return ApplicationError.Validation(
                "schedule.day_closed",
                $"El salón no atiende los {DayName(dayOfWeek).ToLowerInvariant()}.");
        }

        // La cita debe caer enteramente dentro de [FromHour, ToHour).
        // Convertimos start/end a "horas con fracción" del día local
        // para comparar con FromHour/ToHour (que son enteros).
        var startHourLocal = startLocal.Hour + startLocal.Minute / 60.0;
        var endHourLocal = endLocal.Hour + endLocal.Minute / 60.0;

        if (startHourLocal < dayHours.FromHour || endHourLocal > dayHours.ToHour)
        {
            return ApplicationError.Validation(
                "schedule.outside_hours",
                $"La cita ({startLocal:HH:mm}–{endLocal:HH:mm}) cae fuera del horario " +
                $"de atención del salón los {DayName(dayOfWeek).ToLowerInvariant()} " +
                $"({dayHours.FromHour:D2}:00–{dayHours.ToHour:D2}:00).");
        }

        // 5) Lunch break (si está activo) — la cita no puede solaparse
        //    con la franja de almuerzo en absoluto. Solapamiento:
        //    start < lunchEnd && end > lunchStart.
        if (tenant.LunchBreakEnabled)
        {
            var overlapsLunch =
                startHourLocal < tenant.LunchBreakToHour
                && endHourLocal > tenant.LunchBreakFromHour;
            if (overlapsLunch)
            {
                return ApplicationError.Validation(
                    "schedule.lunch_break",
                    $"La cita cae sobre el descanso de almuerzo " +
                    $"({tenant.LunchBreakFromHour:D2}:00–{tenant.LunchBreakToHour:D2}:00).");
            }
        }

        return Result<bool>.Success(true);
    }

    private static string DayName(int dayOfWeek) => dayOfWeek switch
    {
        0 => "Lunes",
        1 => "Martes",
        2 => "Miércoles",
        3 => "Jueves",
        4 => "Viernes",
        5 => "Sábado",
        6 => "Domingo",
        _ => "?",
    };
}
