using BellaSync.Application.Common.Errors;
using BellaSync.Application.Common.Handlers;
using BellaSync.Application.Common.Interfaces;
using BellaSync.Application.Common.Results;
using BellaSync.Application.Features.Tenants.Dtos;
using BellaSync.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BellaSync.Application.Features.Tenants.UpdateSalonHours;

public sealed class UpdateSalonHoursHandler
    : ICommandHandler<UpdateSalonHoursCommand, SalonHoursResponse>
{
    private static readonly TimeSpan ColombiaOffset = TimeSpan.FromHours(-5);

    private readonly IApplicationDbContext _db;
    private readonly ICurrentTenantService _currentTenant;
    private readonly IClock _clock;
    private readonly ILogger<UpdateSalonHoursHandler> _logger;

    public UpdateSalonHoursHandler(
        IApplicationDbContext db,
        ICurrentTenantService currentTenant,
        IClock clock,
        ILogger<UpdateSalonHoursHandler> logger)
    {
        _db = db;
        _currentTenant = currentTenant;
        _clock = clock;
        _logger = logger;
    }

    public async Task<Result<SalonHoursResponse>> HandleAsync(
        UpdateSalonHoursCommand command, CancellationToken ct)
    {
        var tenantId = _currentTenant.TenantId;
        var tenant = await _db.Tenants
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.Id == tenantId, ct);
        if (tenant is null)
            return ApplicationError.NotFound("tenant.not_found", "Salón no encontrado.");

        // 1) Flags del Tenant — el dominio valida from < to si lunchEnabled.
        try
        {
            tenant.UpdateScheduleFlags(
                command.LunchBreakEnabled,
                command.LunchBreakFromHour,
                command.LunchBreakToHour,
                command.IsHolidaysClosed);
        }
        catch (BellaSync.Domain.Common.DomainException ex)
        {
            return ApplicationError.Validation("salon_hours.invalid_flags", ex.Message);
        }

        // 2) Replace-all de SalonWeeklyHours. Borramos todo y recreamos.
        //    EF maneja el DELETE + INSERT en una sola transacción al
        //    SaveChangesAsync. No es óptimo (delta sería menos rows)
        //    pero es simple y correcto.
        var existingHours = await _db.SalonWeeklyHours.ToListAsync(ct);
        _db.SalonWeeklyHours.RemoveRange(existingHours);

        foreach (var (dayOfWeek, range) in command.Days)
        {
            if (range is null) continue;  // día cerrado
            try
            {
                var row = SalonWeeklyHours.Create(
                    tenantId: tenantId,
                    dayOfWeek: dayOfWeek,
                    fromHour: range.FromHour,
                    toHour: range.ToHour);
                _db.SalonWeeklyHours.Add(row);
            }
            catch (BellaSync.Domain.Common.DomainException ex)
            {
                return ApplicationError.Validation(
                    "salon_hours.invalid_day",
                    $"Día {dayOfWeek}: {ex.Message}");
            }
        }

        // 3) Replace-all de SalonClosedDate.
        var existingClosed = await _db.SalonClosedDates.ToListAsync(ct);
        _db.SalonClosedDates.RemoveRange(existingClosed);

        var todayCO = DateOnly.FromDateTime(_clock.UtcNow.Add(ColombiaOffset));
        var seen = new HashSet<DateOnly>();
        foreach (var iso in command.ClosedDates)
        {
            if (!DateOnly.TryParseExact(iso, "yyyy-MM-dd", out var date))
                return ApplicationError.Validation(
                    "salon_hours.bad_date",
                    $"Fecha inválida en closedDates: '{iso}' (esperado YYYY-MM-DD).");
            if (!seen.Add(date)) continue;  // duplicado, lo ignoramos

            try
            {
                var row = SalonClosedDate.Create(tenantId, date, todayCO);
                _db.SalonClosedDates.Add(row);
            }
            catch (BellaSync.Domain.Common.DomainException ex)
            {
                return ApplicationError.Validation("salon_hours.invalid_closed", ex.Message);
            }
        }

        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Horario del tenant {TenantId} actualizado — {OpenDays} días abiertos, {ClosedDates} cierres puntuales",
            tenantId, command.Days.Values.Count(d => d is not null), seen.Count);

        // Re-leer para devolver el estado canónico (con orden ya
        // aplicado, formato consistente).
        var days = new Dictionary<int, DayRange?>();
        var weekly = await _db.SalonWeeklyHours
            .AsNoTracking()
            .OrderBy(h => h.DayOfWeek)
            .ToListAsync(ct);
        for (int d = 0; d < 7; d++)
        {
            var match = weekly.FirstOrDefault(h => h.DayOfWeek == d);
            days[d] = match is null ? null : new DayRange { FromHour = match.FromHour, ToHour = match.ToHour };
        }
        var closed = await _db.SalonClosedDates
            .AsNoTracking()
            .OrderBy(c => c.ClosedDate)
            .Select(c => c.ClosedDate.ToString("yyyy-MM-dd"))
            .ToListAsync(ct);

        return Result<SalonHoursResponse>.Success(new SalonHoursResponse
        {
            Days = days,
            LunchBreakEnabled = tenant.LunchBreakEnabled,
            LunchBreakFromHour = tenant.LunchBreakFromHour,
            LunchBreakToHour = tenant.LunchBreakToHour,
            IsHolidaysClosed = tenant.IsHolidaysClosed,
            ClosedDates = closed,
        });
    }
}
