using BellaSync.Application.Common.Errors;
using BellaSync.Application.Common.Handlers;
using BellaSync.Application.Common.Interfaces;
using BellaSync.Application.Common.Results;
using BellaSync.Application.Features.Subscription.Dtos;
using BellaSync.Application.Features.Subscription.GetSubscription;
using BellaSync.Domain.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BellaSync.Application.Features.Subscription.ChangePlan;

/// <summary>
/// Aplica el cambio de plan. Valida que el plan exista en el catálogo y
/// delega a TenantSubscription.ChangePlan (que rechaza la transición si
/// la suscripción está cancelada).
/// </summary>
public sealed class ChangePlanHandler
    : ICommandHandler<ChangePlanCommand, SubscriptionResponse>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentTenantService _currentTenant;
    private readonly IClock _clock;
    private readonly IQueryHandler<GetSubscriptionQuery, SubscriptionResponse> _getSub;
    private readonly ILogger<ChangePlanHandler> _logger;

    public ChangePlanHandler(
        IApplicationDbContext db,
        ICurrentTenantService currentTenant,
        IClock clock,
        IQueryHandler<GetSubscriptionQuery, SubscriptionResponse> getSub,
        ILogger<ChangePlanHandler> logger)
    {
        _db = db;
        _currentTenant = currentTenant;
        _clock = clock;
        _getSub = getSub;
        _logger = logger;
    }

    public async Task<Result<SubscriptionResponse>> HandleAsync(
        ChangePlanCommand command, CancellationToken ct)
    {
        if (!_currentTenant.HasTenant)
            return ApplicationError.Unauthorized(
                "subscription.no_tenant", "Sesión inválida.");

        if (string.IsNullOrWhiteSpace(command.PlanCode))
            return ApplicationError.Validation(
                "subscription.plan_required", "El plan es obligatorio.");

        var plan = SubscriptionPlanCatalog.Get(command.PlanCode);
        if (plan is null)
            return ApplicationError.Validation(
                "subscription.plan_unknown",
                $"El plan '{command.PlanCode}' no existe.");

        var sub = await _db.TenantSubscriptions
            .FirstOrDefaultAsync(s => s.TenantId == _currentTenant.TenantId, ct);

        if (sub is null)
            return ApplicationError.NotFound(
                "subscription.not_found",
                "El salón no tiene una suscripción activa.");

        try
        {
            sub.ChangePlan(plan.Code, _clock.UtcNow);
        }
        catch (DomainException ex)
        {
            return ApplicationError.Conflict("subscription.change_rejected", ex.Message);
        }

        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Tenant {TenantId} cambió plan a {PlanCode}",
            _currentTenant.TenantId, plan.Code);

        // Reusa el handler de GetSubscription para devolver el snapshot
        // completo — evita duplicar la lógica de armado del response.
        return await _getSub.HandleAsync(new GetSubscriptionQuery(), ct);
    }
}
