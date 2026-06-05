using BellaSync.Application.Common.Errors;
using BellaSync.Application.Common.Handlers;
using BellaSync.Application.Common.Interfaces;
using BellaSync.Application.Common.Results;
using BellaSync.Application.Features.Tenants.Dtos;
using BellaSync.Domain.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BellaSync.Application.Features.Tenants.UpdateReceptionPermissions;

public sealed class UpdateReceptionPermissionsHandler
    : ICommandHandler<UpdateReceptionPermissionsCommand, ReceptionPermissionsResponse>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentTenantService _currentTenant;
    private readonly ILogger<UpdateReceptionPermissionsHandler> _logger;

    public UpdateReceptionPermissionsHandler(
        IApplicationDbContext db,
        ICurrentTenantService currentTenant,
        ILogger<UpdateReceptionPermissionsHandler> logger)
    {
        _db = db;
        _currentTenant = currentTenant;
        _logger = logger;
    }

    public async Task<Result<ReceptionPermissionsResponse>> HandleAsync(
        UpdateReceptionPermissionsCommand command, CancellationToken ct)
    {
        var tenant = await _db.Tenants
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.Id == _currentTenant.TenantId, ct);

        if (tenant is null)
            return ApplicationError.NotFound("tenant.not_found", "Salón no encontrado.");

        try
        {
            tenant.UpdateReceptionPermissions(
                command.ExpenseCapCop,
                command.CanCancelWithMoney,
                command.CanCloseCash,
                command.CanEditStylists,
                command.CanEditServices,
                command.CanEditInventory,
                command.CanViewReports,
                command.CanViewCommissions,
                command.CanEditSchedule,
                command.CanEditPaymentPolicy,
                command.CanEditSalonInfo);
        }
        catch (DomainException ex)
        {
            return ApplicationError.Validation("tenant.invalid_permissions", ex.Message);
        }

        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Permisos de recepción actualizados en tenant {TenantId}.",
            tenant.Id);

        return Result<ReceptionPermissionsResponse>.Success(new ReceptionPermissionsResponse
        {
            ExpenseCapCop = tenant.ReceptionExpenseCapCop,
            CanCancelWithMoney = tenant.ReceptionCanCancelWithMoney,
            CanCloseCash = tenant.ReceptionCanCloseCash,
            CanEditStylists = tenant.ReceptionCanEditStylists,
            CanEditServices = tenant.ReceptionCanEditServices,
            CanEditInventory = tenant.ReceptionCanEditInventory,
            CanViewReports = tenant.ReceptionCanViewReports,
            CanViewCommissions = tenant.ReceptionCanViewCommissions,
            CanEditSchedule = tenant.ReceptionCanEditSchedule,
            CanEditPaymentPolicy = tenant.ReceptionCanEditPaymentPolicy,
            CanEditSalonInfo = tenant.ReceptionCanEditSalonInfo,
        });
    }
}
