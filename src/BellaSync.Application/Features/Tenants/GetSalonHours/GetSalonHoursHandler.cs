using BellaSync.Application.Common.Errors;
using BellaSync.Application.Common.Handlers;
using BellaSync.Application.Common.Interfaces;
using BellaSync.Application.Common.Results;
using BellaSync.Application.Features.Tenants.Dtos;
using Microsoft.EntityFrameworkCore;

namespace BellaSync.Application.Features.Tenants.GetSalonHours;

public sealed class GetSalonHoursHandler
    : IQueryHandler<GetSalonHoursQuery, SalonHoursResponse>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentTenantService _currentTenant;

    public GetSalonHoursHandler(IApplicationDbContext db, ICurrentTenantService currentTenant)
    {
        _db = db;
        _currentTenant = currentTenant;
    }

    public async Task<Result<SalonHoursResponse>> HandleAsync(
        GetSalonHoursQuery query, CancellationToken ct)
    {
        // Tenant para los flags de almuerzo/festivos. IgnoreQueryFilters
        // porque Tenant no es ITenantEntity.
        var tenant = await _db.Tenants
            .IgnoreQueryFilters()
            .Where(t => t.Id == _currentTenant.TenantId)
            .Select(t => new
            {
                t.LunchBreakEnabled,
                t.LunchBreakFromHour,
                t.LunchBreakToHour,
                t.IsHolidaysClosed,
            })
            .FirstOrDefaultAsync(ct);
        if (tenant is null)
            return ApplicationError.NotFound("tenant.not_found", "Salón no encontrado.");

        var weekly = await _db.SalonWeeklyHours
            .AsNoTracking()
            .OrderBy(h => h.DayOfWeek)
            .ToListAsync(ct);

        var closed = await _db.SalonClosedDates
            .AsNoTracking()
            .OrderBy(c => c.ClosedDate)
            .ToListAsync(ct);

        // Construimos el dict 0..6 — todos los días aparecen, los
        // cerrados van como null. El frontend lo necesita así para
        // pintar las 7 filas siempre.
        var days = new Dictionary<int, DayRange?>();
        for (int d = 0; d < 7; d++)
        {
            var match = weekly.FirstOrDefault(h => h.DayOfWeek == d);
            days[d] = match is null ? null : new DayRange
            {
                FromHour = match.FromHour,
                ToHour = match.ToHour,
            };
        }

        return Result<SalonHoursResponse>.Success(new SalonHoursResponse
        {
            Days = days,
            LunchBreakEnabled = tenant.LunchBreakEnabled,
            LunchBreakFromHour = tenant.LunchBreakFromHour,
            LunchBreakToHour = tenant.LunchBreakToHour,
            IsHolidaysClosed = tenant.IsHolidaysClosed,
            ClosedDates = closed.Select(c => c.ClosedDate.ToString("yyyy-MM-dd")).ToList(),
        });
    }
}
