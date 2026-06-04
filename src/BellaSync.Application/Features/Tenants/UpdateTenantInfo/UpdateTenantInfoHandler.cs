using BellaSync.Application.Common.Errors;
using BellaSync.Application.Common.Handlers;
using BellaSync.Application.Common.Interfaces;
using BellaSync.Application.Common.Results;
using BellaSync.Application.Features.Tenants.Dtos;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BellaSync.Application.Features.Tenants.UpdateTenantInfo;

public sealed class UpdateTenantInfoHandler
    : ICommandHandler<UpdateTenantInfoCommand, TenantInfoResponse>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentTenantService _currentTenant;
    private readonly ILogger<UpdateTenantInfoHandler> _logger;

    public UpdateTenantInfoHandler(
        IApplicationDbContext db,
        ICurrentTenantService currentTenant,
        ILogger<UpdateTenantInfoHandler> logger)
    {
        _db = db;
        _currentTenant = currentTenant;
        _logger = logger;
    }

    public async Task<Result<TenantInfoResponse>> HandleAsync(
        UpdateTenantInfoCommand command, CancellationToken ct)
    {
        var tenant = await _db.Tenants
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.Id == _currentTenant.TenantId, ct);

        if (tenant is null)
            return ApplicationError.NotFound("tenant.not_found", "Salón no encontrado.");

        try
        {
            tenant.Rename(command.Name);
            tenant.UpdateInfo(
                address: command.Address,
                phone: command.Phone,
                contactEmail: command.ContactEmail,
                logoUrl: command.LogoUrl,
                instagramHandle: command.InstagramHandle,
                description: command.Description);
        }
        catch (BellaSync.Domain.Common.DomainException ex)
        {
            return ApplicationError.Validation("tenant_info.invalid", ex.Message);
        }

        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Tenant {TenantId} info actualizada", tenant.Id);

        return Result<TenantInfoResponse>.Success(new TenantInfoResponse
        {
            Name = tenant.Name,
            Slug = tenant.Slug,
            Address = tenant.Address,
            Phone = tenant.Phone,
            ContactEmail = tenant.ContactEmail,
            LogoUrl = tenant.LogoUrl,
            InstagramHandle = tenant.InstagramHandle,
            Description = tenant.Description,
        });
    }
}
