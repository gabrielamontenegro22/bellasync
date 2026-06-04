using BellaSync.Application.Common.Errors;
using BellaSync.Application.Common.Handlers;
using BellaSync.Application.Common.Interfaces;
using BellaSync.Application.Common.Results;
using BellaSync.Application.Features.Tenants.Dtos;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BellaSync.Application.Features.Tenants.UpdateCommissionsSetting;

public sealed class UpdateCommissionsSettingHandler
    : ICommandHandler<UpdateCommissionsSettingCommand, CommissionsSettingResponse>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentTenantService _currentTenant;
    private readonly ILogger<UpdateCommissionsSettingHandler> _logger;

    public UpdateCommissionsSettingHandler(
        IApplicationDbContext db,
        ICurrentTenantService currentTenant,
        ILogger<UpdateCommissionsSettingHandler> logger)
    {
        _db = db;
        _currentTenant = currentTenant;
        _logger = logger;
    }

    public async Task<Result<CommissionsSettingResponse>> HandleAsync(
        UpdateCommissionsSettingCommand command, CancellationToken ct)
    {
        var tenant = await _db.Tenants
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.Id == _currentTenant.TenantId, ct);

        if (tenant is null)
            return ApplicationError.NotFound("tenant.not_found", "Salón no encontrado.");

        tenant.SetCommissionsEnabled(command.Enabled);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Comisiones {Action} para tenant {TenantId}",
            command.Enabled ? "activadas" : "desactivadas", tenant.Id);

        return Result<CommissionsSettingResponse>.Success(
            new CommissionsSettingResponse { Enabled = tenant.CommissionsEnabled });
    }
}
