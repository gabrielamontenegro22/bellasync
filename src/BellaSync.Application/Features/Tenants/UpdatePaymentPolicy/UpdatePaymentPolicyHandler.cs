using BellaSync.Application.Common.Errors;
using BellaSync.Application.Common.Handlers;
using BellaSync.Application.Common.Interfaces;
using BellaSync.Application.Common.Results;
using BellaSync.Application.Features.Tenants.Dtos;
using BellaSync.Domain.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BellaSync.Application.Features.Tenants.UpdatePaymentPolicy;

public sealed class UpdatePaymentPolicyHandler
    : ICommandHandler<UpdatePaymentPolicyCommand, TenantPaymentPolicyResponse>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentTenantService _currentTenant;
    private readonly ILogger<UpdatePaymentPolicyHandler> _logger;

    public UpdatePaymentPolicyHandler(
        IApplicationDbContext db,
        ICurrentTenantService currentTenant,
        ILogger<UpdatePaymentPolicyHandler> logger)
    {
        _db = db;
        _currentTenant = currentTenant;
        _logger = logger;
    }

    public async Task<Result<TenantPaymentPolicyResponse>> HandleAsync(
        UpdatePaymentPolicyCommand command, CancellationToken ct)
    {
        var tenant = await _db.Tenants
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.Id == _currentTenant.TenantId, ct);

        if (tenant is null)
            return ApplicationError.NotFound("tenant.not_found", "Salón no encontrado.");

        try
        {
            tenant.UpdatePaymentPolicy(
                command.HoldDurationHours,
                command.HoldMinBeforeAppointmentMinutes,
                command.MinAdvanceMinutes);
        }
        catch (DomainException ex)
        {
            return ApplicationError.Validation("tenant.invalid_policy", ex.Message);
        }

        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Política de pagos actualizada en tenant {TenantId}: hold={Hold}h, holdBefore={Before}min, minAdvance={Advance}min",
            tenant.Id, command.HoldDurationHours,
            command.HoldMinBeforeAppointmentMinutes, command.MinAdvanceMinutes);

        return Result<TenantPaymentPolicyResponse>.Success(new TenantPaymentPolicyResponse
        {
            HoldDurationHours = tenant.HoldDurationHours,
            HoldMinBeforeAppointmentMinutes = tenant.HoldMinBeforeAppointmentMinutes,
            MinAdvanceMinutes = tenant.MinAdvanceMinutes,
        });
    }
}
