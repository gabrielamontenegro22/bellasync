using BellaSync.Application.Common.Errors;
using BellaSync.Application.Common.Handlers;
using BellaSync.Application.Common.Interfaces;
using BellaSync.Application.Common.Results;
using BellaSync.Application.Features.Subscription.Dtos;
using BellaSync.Application.Features.Subscription.GetSubscription;
using BellaSync.Domain.Common;
using BellaSync.Domain.Entities;
using BellaSync.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BellaSync.Application.Features.Subscription.ChangePlan;

/// <summary>
/// Aplica el cambio de plan + emite factura prorrateada si es UPGRADE.
///
/// Reglas:
///   - El cambio del PlanCode es inmediato (no espera al próximo ciclo).
///   - Si el plan nuevo es más caro Y la sub está Active con días
///     restantes en el período, emite una factura Pending por el
///     prorrateo (diff × días-restantes / 30). El admin debe pagar
///     esa factura para no quedar PastDue.
///   - Downgrade: no se emite nada ni se reembolsa. La diferencia
///     simplemente aplica al próximo ciclo.
///   - Trial: no se emite factura prorrateada — el trial sigue siendo
///     gratis con el plan nuevo hasta que termine.
///   - PastDue: no se emite factura adicional — ya tiene una pendiente.
///   - Cancelled: rechazado por la entidad.
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

        var newPlan = SubscriptionPlanCatalog.Get(command.PlanCode);
        if (newPlan is null)
            return ApplicationError.Validation(
                "subscription.plan_unknown",
                $"El plan '{command.PlanCode}' no existe.");

        var sub = await _db.TenantSubscriptions
            .FirstOrDefaultAsync(s => s.TenantId == _currentTenant.TenantId, ct);

        if (sub is null)
            return ApplicationError.NotFound(
                "subscription.not_found",
                "El salón no tiene una suscripción activa.");

        var oldPlan = SubscriptionPlanCatalog.Get(sub.PlanCode);
        var now = _clock.UtcNow;

        try
        {
            sub.ChangePlan(newPlan.Code, now);
        }
        catch (DomainException ex)
        {
            return ApplicationError.Conflict("subscription.change_rejected", ex.Message);
        }

        // Prorrateo: solo en upgrade real de Active. Trial / PastDue /
        // Cancelled no aplican (los dos primeros no facturan extra ahora;
        // el último ya está bloqueado por la entidad).
        if (sub.Status == SubscriptionStatus.Active
            && oldPlan is not null
            && newPlan.MonthlyPrice > oldPlan.MonthlyPrice)
        {
            var daysRemaining = (sub.CurrentPeriodEnd - now).TotalDays;
            var charge = SubscriptionPlanCatalog.ComputeProratedUpgradeCharge(
                oldPlan.MonthlyPrice, newPlan.MonthlyPrice, daysRemaining);

            if (charge > 0)
            {
                var invoice = SubscriptionInvoice.Issue(
                    tenantId: _currentTenant.TenantId,
                    planCode: newPlan.Code,
                    amount: Money.Create(charge),
                    periodStart: now,
                    periodEnd: sub.CurrentPeriodEnd,
                    dueDate: now.AddDays(7),
                    utcNow: now);
                _db.SubscriptionInvoices.Add(invoice);

                _logger.LogInformation(
                    "Tenant {TenantId} upgrade {Old}→{New}: factura prorrateada ${Charge} ({Days:F1} días)",
                    _currentTenant.TenantId, oldPlan.Code, newPlan.Code, charge, daysRemaining);
            }
        }

        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Tenant {TenantId} cambió plan a {PlanCode}",
            _currentTenant.TenantId, newPlan.Code);

        return await _getSub.HandleAsync(new GetSubscriptionQuery(), ct);
    }
}
