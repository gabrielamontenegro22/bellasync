using BellaSync.Application.Common.Errors;
using BellaSync.Application.Common.Handlers;
using BellaSync.Application.Common.Interfaces;
using BellaSync.Application.Common.Results;
using BellaSync.Application.Features.Tenants.Dtos;
using Microsoft.EntityFrameworkCore;

namespace BellaSync.Application.Features.Tenants.GetTenantInfo;

public sealed class GetTenantInfoHandler
    : IQueryHandler<GetTenantInfoQuery, TenantInfoResponse>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentTenantService _currentTenant;

    public GetTenantInfoHandler(IApplicationDbContext db, ICurrentTenantService currentTenant)
    {
        _db = db;
        _currentTenant = currentTenant;
    }

    public async Task<Result<TenantInfoResponse>> HandleAsync(
        GetTenantInfoQuery query, CancellationToken ct)
    {
        var info = await _db.Tenants
            .IgnoreQueryFilters()
            .Where(t => t.Id == _currentTenant.TenantId)
            .Select(t => new TenantInfoResponse
            {
                Name = t.Name,
                Slug = t.Slug,
                Address = t.Address,
                Phone = t.Phone,
                ContactEmail = t.ContactEmail,
                LogoUrl = t.LogoUrl,
                InstagramHandle = t.InstagramHandle,
                Description = t.Description,
            })
            .FirstOrDefaultAsync(ct);

        if (info is null)
            return ApplicationError.NotFound("tenant.not_found", "Salón no encontrado.");

        return Result<TenantInfoResponse>.Success(info);
    }
}
