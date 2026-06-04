using BellaSync.Domain.Common;
using BellaSync.Domain.ValueObjects;

namespace BellaSync.Domain.Entities;

/// <summary>
/// Factura mensual de la suscripción de un tenant a BellaSync. Una se
/// genera cada mes al cumplirse el período. Cuando se paga (manual por
/// SaaSAdmin o vía pasarela en el futuro), se llama a MarkPaid() y eso
/// extiende la suscripción +1 mes.
///
/// Setters privados — toda mutación pasa por métodos verbales.
/// </summary>
public class SubscriptionInvoice : BaseEntity, ITenantEntity
{
    private SubscriptionInvoice() { }

    /// <summary>
    /// Crea una factura nueva para un período. Llamada por el job mensual
    /// al renovar la suscripción.
    /// </summary>
    public static SubscriptionInvoice Issue(
        Guid tenantId,
        string planCode,
        Money amount,
        DateTime periodStart,
        DateTime periodEnd,
        DateTime dueDate,
        DateTime utcNow)
    {
        if (tenantId == Guid.Empty)
            throw new DomainException("TenantId es obligatorio.");
        if (string.IsNullOrWhiteSpace(planCode))
            throw new DomainException("PlanCode es obligatorio.");
        if (amount.Amount <= 0m)
            throw new DomainException("El monto debe ser mayor a cero.");
        if (periodEnd <= periodStart)
            throw new DomainException("PeriodEnd debe ser posterior a PeriodStart.");

        return new SubscriptionInvoice
        {
            TenantId = tenantId,
            PlanCode = planCode.Trim().ToLowerInvariant(),
            Amount = amount,
            PeriodStart = periodStart,
            PeriodEnd = periodEnd,
            DueDate = dueDate,
            IssuedAt = utcNow,
            Status = SubscriptionInvoiceStatus.Pending,
        };
    }

    public Guid TenantId { get; set; }

    /// <summary>Snapshot del plan al momento de emisión.</summary>
    public string PlanCode { get; private set; } = string.Empty;

    public Money Amount { get; private set; } = Money.Zero;

    public DateTime PeriodStart { get; private set; }
    public DateTime PeriodEnd { get; private set; }
    public DateTime DueDate { get; private set; }

    public DateTime IssuedAt { get; private set; }

    public SubscriptionInvoiceStatus Status { get; private set; }

    public DateTime? PaidAt { get; private set; }

    /// <summary>"Cash" | "Transfer" | "Card" | "Other" — método con que
    /// se pagó. null mientras Status=Pending/Failed.</summary>
    public string? PaymentMethod { get; private set; }

    /// <summary>Banco/billetera o referencia interna del pago.</summary>
    public string? Reference { get; private set; }

    /// <summary>Razón cuando Status=Failed o Waived.</summary>
    public string? Note { get; private set; }

    // ===== MÉTODOS VERBALES =====

    public void MarkPaid(string method, string? reference, DateTime utcNow)
    {
        if (Status == SubscriptionInvoiceStatus.Paid) return;
        if (Status == SubscriptionInvoiceStatus.Waived)
            throw new DomainException("Una factura Waived no puede marcarse Paid.");
        if (string.IsNullOrWhiteSpace(method))
            throw new DomainException("El método de pago es obligatorio.");

        Status = SubscriptionInvoiceStatus.Paid;
        PaidAt = utcNow;
        PaymentMethod = method.Trim();
        Reference = string.IsNullOrWhiteSpace(reference) ? null : reference.Trim();
        UpdatedAt = utcNow;
    }

    public void MarkFailed(string reason, DateTime utcNow)
    {
        if (string.IsNullOrWhiteSpace(reason))
            throw new DomainException("La razón del fallo es obligatoria.");
        Status = SubscriptionInvoiceStatus.Failed;
        Note = reason.Trim();
        UpdatedAt = utcNow;
    }

    public void Waive(string reason, DateTime utcNow)
    {
        if (Status == SubscriptionInvoiceStatus.Paid)
            throw new DomainException("Una factura ya pagada no puede ser Waived.");
        if (string.IsNullOrWhiteSpace(reason))
            throw new DomainException("La razón del waive es obligatoria.");

        Status = SubscriptionInvoiceStatus.Waived;
        Note = reason.Trim();
        UpdatedAt = utcNow;
    }
}
