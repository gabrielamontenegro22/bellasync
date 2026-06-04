using BellaSync.Application.Common.Errors;
using BellaSync.Application.Common.Handlers;
using BellaSync.Application.Common.Interfaces;
using BellaSync.Application.Common.Results;
using BellaSync.Application.Features.Tenants.Dtos;
using Microsoft.EntityFrameworkCore;

namespace BellaSync.Application.Features.Tenants.GetCommissionsSetting;

public sealed class GetCommissionsSettingHandler
    : IQueryHandler<GetCommissionsSettingQuery, CommissionsSettingResponse>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentTenantService _currentTenant;

    public GetCommissionsSettingHandler(IApplicationDbContext db, ICurrentTenantService currentTenant)
    {
        _db = db;
        _currentTenant = currentTenant;
    }

    public async Task<Result<CommissionsSettingResponse>> HandleAsync(
        GetCommissionsSettingQuery query, CancellationToken ct)
    {
        // IgnoreQueryFilters: el filtro multi-tenant tiraría 0 filas porque
        // Tenant no es ITenantEntity (es la propia tabla de tenants).
        var enabled = await _db.Tenants
            .IgnoreQueryFilters()
            .Where(t => t.Id == _currentTenant.TenantId)
            .Select(t => (bool?)t.CommissionsEnabled)
            .FirstOrDefaultAsync(ct);

        if (enabled is null)
            return ApplicationError.NotFound("tenant.not_found", "Salón no encontrado.");

        return Result<CommissionsSettingResponse>.Success(
            new CommissionsSettingResponse { Enabled = enabled.Value });
    }
}
