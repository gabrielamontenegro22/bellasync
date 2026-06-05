using BellaSync.Application.Common.Errors;
using BellaSync.Application.Common.Handlers;
using BellaSync.Application.Common.Interfaces;
using BellaSync.Application.Common.Results;
using BellaSync.Application.Features.Tenants.Dtos;
using Microsoft.EntityFrameworkCore;

namespace BellaSync.Application.Features.Tenants.GetPaymentPolicy;

public sealed class GetPaymentPolicyHandler
    : IQueryHandler<GetPaymentPolicyQuery, TenantPaymentPolicyResponse>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentTenantService _currentTenant;

    public GetPaymentPolicyHandler(IApplicationDbContext db, ICurrentTenantService currentTenant)
    {
        _db = db;
        _currentTenant = currentTenant;
    }

    public async Task<Result<TenantPaymentPolicyResponse>> HandleAsync(
        GetPaymentPolicyQuery query, CancellationToken ct)
    {
        // IgnoreQueryFilters porque el filtro global multi-tenant filtra
        // por TenantId y nosotros estamos consultando el propio tenant.
        var tenant = await _db.Tenants
            .IgnoreQueryFilters()
            .Where(t => t.Id == _currentTenant.TenantId)
            .Select(t => new TenantPaymentPolicyResponse
            {
                HoldDurationHours = t.HoldDurationHours,
                HoldMinBeforeAppointmentMinutes = t.HoldMinBeforeAppointmentMinutes,
                MinAdvanceMinutes = t.MinAdvanceMinutes,
                CancellationWindowHours = t.CancellationWindowHours,
            })
            .FirstOrDefaultAsync(ct);

        if (tenant is null)
            return ApplicationError.NotFound("tenant.not_found", "Salón no encontrado.");

        return Result<TenantPaymentPolicyResponse>.Success(tenant);
    }
}
