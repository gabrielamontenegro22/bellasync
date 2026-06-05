using BellaSync.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace BellaSync.Infrastructure.Services;

/// <summary>
/// Lee los permisos desde la tabla tenants. Cachea por request — la
/// primera llamada hace 1 query, las siguientes en el mismo request
/// reutilizan el snapshot. Multi-request no cachea: cada nuevo request
/// crea su scope nuevo (porque DI es scoped).
/// </summary>
public class ReceptionPermissionsService : IReceptionPermissionsService
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentTenantService _currentTenant;
    private ReceptionPermissionsSnapshot? _cached;

    public ReceptionPermissionsService(
        IApplicationDbContext db,
        ICurrentTenantService currentTenant)
    {
        _db = db;
        _currentTenant = currentTenant;
    }

    public async Task<ReceptionPermissionsSnapshot> GetAsync(CancellationToken ct)
    {
        if (_cached is not null) return _cached;

        var tenantId = _currentTenant.TenantId;
        if (tenantId == Guid.Empty)
        {
            // Sin tenant (SuperAdmin o request anónimo) → todo restrictivo.
            _cached = new ReceptionPermissionsSnapshot(
                ExpenseCapCop: 0m,
                CanCancelWithMoney: false,
                CanCloseCash: false,
                CanEditStylists: false,
                CanEditServices: false,
                CanViewReports: false,
                CanViewCommissions: false,
                CanEditSchedule: false,
                CanEditPaymentPolicy: false,
                CanEditSalonInfo: false);
            return _cached;
        }

        // IgnoreQueryFilters porque el filtro global multi-tenant filtra
        // por TenantId pero estamos leyendo el propio tenant — el Where
        // limita seguridad igual.
        _cached = await _db.Tenants
            .AsNoTracking()
            .IgnoreQueryFilters()
            .Where(t => t.Id == tenantId)
            .Select(t => new ReceptionPermissionsSnapshot(
                t.ReceptionExpenseCapCop,
                t.ReceptionCanCancelWithMoney,
                t.ReceptionCanCloseCash,
                t.ReceptionCanEditStylists,
                t.ReceptionCanEditServices,
                t.ReceptionCanViewReports,
                t.ReceptionCanViewCommissions,
                t.ReceptionCanEditSchedule,
                t.ReceptionCanEditPaymentPolicy,
                t.ReceptionCanEditSalonInfo))
            .FirstOrDefaultAsync(ct)
            ?? new ReceptionPermissionsSnapshot(
                0m, false, false, false, false, false, false, false, false, false);

        return _cached;
    }
}
