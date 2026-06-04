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

        // Guard: si hay una factura Reported (pago reportado esperando
        // validación del SuperAdmin), bloqueamos el cambio. Sería confuso
        // y peligroso permitir cambiar plan mientras un pago del plan
        // viejo está en proceso bancario — la referencia que el salón
        // reportó es por un monto del plan viejo, validar tras cambio
        // dejaría inconsistencia (pagó X, le activamos Y).
        var hasReported = await _db.SubscriptionInvoices
            .AnyAsync(i => i.TenantId == _currentTenant.TenantId
                        && i.Status == SubscriptionInvoiceStatus.Reported, ct);

        if (hasReported)
            return ApplicationError.Conflict(
                "subscription.payment_in_validation",
                "No puedes cambiar el plan mientras tu pago anterior está en validación. " +
                "Espera la decisión de BellaSync (1–2 días hábiles).");

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

        // Actualizar Pending viejas: el dispatcher pudo haber emitido
        // una factura del próximo período con el plan VIEJO (caso típico:
        // 7 días antes del fin del ciclo). Si el salón cambia plan antes
        // de reportar, la factura debe pasar al plan nuevo para que el
        // monto que reporte coincida con lo que va a cobrar.
        //
        // Solo tocamos Pending del PRÓXIMO ciclo (PeriodStart >=
        // CurrentPeriodEnd): NO tocamos facturas prorrateadas (que viven
        // DENTRO del ciclo actual) — bug C3 del audit: antes pisábamos la
        // factura prorrateada del upgrade que María recién hizo,
        // convirtiéndola en "basic $50k" si después downgradeaba.
        //
        // Reported ya bloqueamos arriba; Paid/Failed/Waived son inmutables.
        var pendingInvoices = await _db.SubscriptionInvoices
            .Where(i => i.TenantId == _currentTenant.TenantId
                     && i.Status == SubscriptionInvoiceStatus.Pending
                     && i.PlanCode != newPlan.Code
                     && i.PeriodStart >= sub.CurrentPeriodEnd)
            .ToListAsync(ct);

        foreach (var pending in pendingInvoices)
        {
            try
            {
                pending.UpdatePlanInfo(newPlan.Code, Money.Create(newPlan.MonthlyPrice), now);
                _logger.LogInformation(
                    "Tenant {TenantId} factura {InvoiceId} actualizada: {OldPlan}→{NewPlan} (${Amount})",
                    _currentTenant.TenantId, pending.Id, pending.PlanCode, newPlan.Code, newPlan.MonthlyPrice);
            }
            catch (DomainException ex)
            {
                // No debería pasar (filtramos por Pending) pero defensivo:
                // logueamos y seguimos para no romper el cambio de plan
                // por una factura corrupta.
                _logger.LogWarning(ex,
                    "No se pudo actualizar factura {InvoiceId} al cambiar plan",
                    pending.Id);
            }
        }

        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Tenant {TenantId} cambió plan a {PlanCode}",
            _currentTenant.TenantId, newPlan.Code);

        return await _getSub.HandleAsync(new GetSubscriptionQuery(), ct);
    }
}
