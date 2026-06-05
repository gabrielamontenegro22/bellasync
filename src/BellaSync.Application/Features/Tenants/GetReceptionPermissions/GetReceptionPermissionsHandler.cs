using BellaSync.Application.Common.Errors;
using BellaSync.Application.Common.Handlers;
using BellaSync.Application.Common.Interfaces;
using BellaSync.Application.Common.Results;
using BellaSync.Application.Features.Tenants.Dtos;
using Microsoft.EntityFrameworkCore;

namespace BellaSync.Application.Features.Tenants.GetReceptionPermissions;

public sealed class GetReceptionPermissionsHandler
    : IQueryHandler<GetReceptionPermissionsQuery, ReceptionPermissionsResponse>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentTenantService _currentTenant;

    public GetReceptionPermissionsHandler(
        IApplicationDbContext db,
        ICurrentTenantService currentTenant)
    {
        _db = db;
        _currentTenant = currentTenant;
    }

    public async Task<Result<ReceptionPermissionsResponse>> HandleAsync(
        GetReceptionPermissionsQuery query, CancellationToken ct)
    {
        // IgnoreQueryFilters: el filtro global multi-tenant filtra por
        // TenantId, pero queremos leer el propio tenant actual. Igual
        // limitamos por _currentTenant.TenantId para seguridad.
        var perms = await _db.Tenants
            .IgnoreQueryFilters()
            .Where(t => t.Id == _currentTenant.TenantId)
            .Select(t => new ReceptionPermissionsResponse
            {
                ExpenseCapCop = t.ReceptionExpenseCapCop,
                CanCancelWithMoney = t.ReceptionCanCancelWithMoney,
                CanCloseCash = t.ReceptionCanCloseCash,
                CanEditStylists = t.ReceptionCanEditStylists,
                CanEditServices = t.ReceptionCanEditServices,
                CanEditInventory = t.ReceptionCanEditInventory,
                CanViewReports = t.ReceptionCanViewReports,
                CanViewCommissions = t.ReceptionCanViewCommissions,
                CanEditSchedule = t.ReceptionCanEditSchedule,
                CanEditPaymentPolicy = t.ReceptionCanEditPaymentPolicy,
                CanEditSalonInfo = t.ReceptionCanEditSalonInfo,
            })
            .FirstOrDefaultAsync(ct);

        if (perms is null)
            return ApplicationError.NotFound("tenant.not_found", "Salón no encontrado.");

        return Result<ReceptionPermissionsResponse>.Success(perms);
    }
}
