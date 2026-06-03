using BellaSync.Application.Common.Interfaces;
using BellaSync.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace BellaSync.Infrastructure.Services;

/// <summary>
/// Implementación scoped: lee la política de pagos del tenant del
/// request actual y la cachea durante el scope (= durante todo el
/// pipeline del request HTTP). Así si un handler pide los 3 valores
/// hace solo 1 query a la BD, no 3.
///
/// Fallback: si el tenant no se encuentra (background jobs / tests),
/// devuelve los defaults históricos (3 / 30 / 30).
/// </summary>
public sealed class TenantAppointmentSettingsService : ITenantAppointmentSettings
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentTenantService _currentTenant;

    private CachedSettings? _cache;

    public TenantAppointmentSettingsService(
        IApplicationDbContext db,
        ICurrentTenantService currentTenant)
    {
        _db = db;
        _currentTenant = currentTenant;
    }

    public async Task<int> GetHoldDurationHoursAsync(CancellationToken ct)
        => (await LoadAsync(ct)).HoldDurationHours;

    public async Task<int> GetHoldMinBeforeAppointmentMinutesAsync(CancellationToken ct)
        => (await LoadAsync(ct)).HoldMinBeforeAppointmentMinutes;

    public async Task<int> GetMinAdvanceMinutesAsync(CancellationToken ct)
        => (await LoadAsync(ct)).MinAdvanceMinutes;

    private async Task<CachedSettings> LoadAsync(CancellationToken ct)
    {
        if (_cache is not null) return _cache;

        var tenantId = _currentTenant.TenantId;
        if (tenantId == Guid.Empty)
        {
            _cache = new CachedSettings(3, 30, 30);
            return _cache;
        }

        // Bypass del global filter porque CurrentTenantService a veces
        // devuelve un tenantId pero el filter ya lo aplicó. Buscamos
        // directo. Solo leemos columnas escalares — no traemos navigations.
        var row = await _db.Tenants
            .IgnoreQueryFilters()
            .Where(t => t.Id == tenantId)
            .Select(t => new CachedSettings(
                t.HoldDurationHours,
                t.HoldMinBeforeAppointmentMinutes,
                t.MinAdvanceMinutes))
            .FirstOrDefaultAsync(ct);

        _cache = row ?? new CachedSettings(3, 30, 30);
        return _cache;
    }

    private record CachedSettings(int HoldDurationHours, int HoldMinBeforeAppointmentMinutes, int MinAdvanceMinutes);
}
