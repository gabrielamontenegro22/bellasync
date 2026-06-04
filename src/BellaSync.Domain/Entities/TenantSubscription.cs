using BellaSync.Domain.Common;

namespace BellaSync.Domain.Entities;

/// <summary>
/// La suscripción del salón (tenant) al SaaS BellaSync. 1:1 con Tenant.
///
/// Setters privados — toda mutación pasa por métodos verbales que
/// validan invariantes (no se puede saltar Trial → Cancelled sin
/// pasar por Active, por ejemplo).
///
/// Notas de diseño:
///   - El precio NO se guarda acá — vive en SubscriptionPlanCatalog
///     (estático en C#). Si los precios cambian, lo nuevo aplica solo
///     a los próximos cobros, lo ya cobrado queda en SubscriptionInvoice.
///   - CurrentPeriodEnd se actualiza cuando se paga una factura
///     (renueva +1 mes). Si pasa la fecha sin pago, el background job
///     marca PastDue.
///   - El trial no genera SubscriptionInvoice — es free. Solo cuando
///     transiciona a Active se crea la primera factura.
/// </summary>
public class TenantSubscription : BaseEntity, ITenantEntity
{
    private TenantSubscription() { }

    /// <summary>
    /// Crea una suscripción nueva en Trial de N días con el plan default
    /// del catálogo. Llamado al crear el tenant (en OnboardingWizard).
    /// </summary>
    public static TenantSubscription StartTrial(
        Guid tenantId,
        string planCode,
        int trialDays,
        DateTime utcNow)
    {
        if (tenantId == Guid.Empty)
            throw new DomainException("TenantId es obligatorio.");
        if (string.IsNullOrWhiteSpace(planCode))
            throw new DomainException("PlanCode es obligatorio.");
        if (trialDays < 0 || trialDays > 90)
            throw new DomainException("TrialDays debe ser entre 0 y 90.");

        return new TenantSubscription
        {
            TenantId = tenantId,
            PlanCode = planCode.Trim().ToLowerInvariant(),
            Status = trialDays > 0 ? SubscriptionStatus.Trial : SubscriptionStatus.Active,
            StartedAt = utcNow,
            // En trial, "fin de período" = fin del trial. Después de activar
            // se reemplaza por la próxima fecha de cobro.
            CurrentPeriodEnd = utcNow.AddDays(trialDays),
            TrialEndsAt = trialDays > 0 ? utcNow.AddDays(trialDays) : null,
        };
    }

    public Guid TenantId { get; set; }

    /// <summary>Code del plan en el catálogo ("basic", "professional", "premium").</summary>
    public string PlanCode { get; private set; } = string.Empty;

    public SubscriptionStatus Status { get; private set; }

    public DateTime StartedAt { get; private set; }

    /// <summary>
    /// Fecha en que vence el período actual. Cuando se paga una factura,
    /// se extiende +1 mes. Si pasa sin pago, transiciona a PastDue.
    /// </summary>
    public DateTime CurrentPeriodEnd { get; private set; }

    /// <summary>Fin del período de prueba (null si nunca fue trial).</summary>
    public DateTime? TrialEndsAt { get; private set; }

    /// <summary>Cuándo se canceló (null si no está cancelada).</summary>
    public DateTime? CancelledAt { get; private set; }

    public string? CancellationReason { get; private set; }

    // ===== MÉTODOS VERBALES =====

    /// <summary>
    /// Cambia el plan. Para v1 el cambio es inmediato (no prorratea).
    /// El próximo cobro será al precio del plan nuevo.
    /// </summary>
    public void ChangePlan(string newPlanCode, DateTime utcNow)
    {
        if (string.IsNullOrWhiteSpace(newPlanCode))
            throw new DomainException("PlanCode es obligatorio.");
        if (Status == SubscriptionStatus.Cancelled)
            throw new DomainException("No se puede cambiar el plan de una suscripción cancelada.");

        var normalized = newPlanCode.Trim().ToLowerInvariant();
        if (PlanCode == normalized) return;  // idempotente

        PlanCode = normalized;
        UpdatedAt = utcNow;
    }

    /// <summary>
    /// Transición Trial → Active al recibir el primer pago. La fecha
    /// del próximo cobro se computa como now + 1 mes.
    ///
    /// IMPORTANTE: preserva TrialEndsAt — es info histórica útil para
    /// reportes de conversión ("cuántos clientes convirtieron desde el
    /// trial"). No se borra al activar.
    /// </summary>
    public void Activate(DateTime utcNow)
    {
        if (Status == SubscriptionStatus.Active) return;
        if (Status == SubscriptionStatus.Cancelled)
            throw new DomainException("No se puede activar una suscripción cancelada.");

        Status = SubscriptionStatus.Active;
        CurrentPeriodEnd = utcNow.AddMonths(1);
        UpdatedAt = utcNow;
    }

    /// <summary>
    /// Marca como PastDue cuando se pasa el período sin pago. El acceso
    /// no se bloquea automáticamente — la admin del salón debería
    /// recibir notificación.
    /// </summary>
    public void MarkPastDue(DateTime utcNow)
    {
        if (Status != SubscriptionStatus.Active && Status != SubscriptionStatus.Trial)
            return;
        Status = SubscriptionStatus.PastDue;
        UpdatedAt = utcNow;
    }

    /// <summary>
    /// Renueva el período al recibir pago. PastDue/Active → Active +
    /// extiende CurrentPeriodEnd a now + 1 mes.
    /// </summary>
    public void Renew(DateTime utcNow)
    {
        if (Status == SubscriptionStatus.Cancelled)
            throw new DomainException("No se puede renovar una suscripción cancelada.");

        Status = SubscriptionStatus.Active;
        // Si el período anterior no había vencido aún, extendemos desde
        // CurrentPeriodEnd. Si ya venció, partimos de hoy.
        var base_ = CurrentPeriodEnd > utcNow ? CurrentPeriodEnd : utcNow;
        CurrentPeriodEnd = base_.AddMonths(1);
        UpdatedAt = utcNow;
    }

    /// <summary>Cancela la suscripción. Acceso a nuevas features cesa.</summary>
    public void Cancel(string? reason, DateTime utcNow)
    {
        if (Status == SubscriptionStatus.Cancelled) return;
        Status = SubscriptionStatus.Cancelled;
        CancelledAt = utcNow;
        CancellationReason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
        UpdatedAt = utcNow;
    }
}
