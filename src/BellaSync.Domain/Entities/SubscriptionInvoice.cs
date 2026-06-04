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

    /// <summary>Razón cuando Status=Failed o Waived, o rejection reason.</summary>
    public string? Note { get; private set; }

    // ===== Reporte (paso intermedio: salón dice que pagó) =====

    /// <summary>Cuándo el SalonAdmin reportó el pago.</summary>
    public DateTime? ReportedAt { get; private set; }

    /// <summary>Método reportado ("Bancolombia", "Nequi", etc.).</summary>
    public string? ReportedMethod { get; private set; }

    /// <summary>Referencia del comprobante reportada por el salón.</summary>
    public string? ReportedReference { get; private set; }

    // ===== Validación (decisión del SuperAdmin) =====

    /// <summary>UserId del SuperAdmin que validó.</summary>
    public Guid? ValidatedByUserId { get; private set; }
    public DateTime? ValidatedAt { get; private set; }

    /// <summary>Cuándo fue rechazada (vuelve a Pending pero queda traza).</summary>
    public DateTime? RejectedAt { get; private set; }

    // ===== MÉTODOS VERBALES =====

    /// <summary>
    /// Paso 1 del flujo anti-pasarela: el SalonAdmin reporta haber
    /// transferido el dinero. La factura pasa a Reported y queda
    /// esperando que el SuperAdmin verifique contra el banco.
    /// La suscripción NO se activa todavía.
    ///
    /// Solo válido desde Pending. Si la factura ya está Reported,
    /// permite re-reportar (override de método/referencia).
    /// </summary>
    public void ReportPayment(string method, string? reference, DateTime utcNow)
    {
        if (Status != SubscriptionInvoiceStatus.Pending
            && Status != SubscriptionInvoiceStatus.Reported)
            throw new DomainException(
                "Solo se puede reportar pago sobre facturas pendientes.");
        if (string.IsNullOrWhiteSpace(method))
            throw new DomainException("El método de pago es obligatorio.");

        Status = SubscriptionInvoiceStatus.Reported;
        ReportedAt = utcNow;
        ReportedMethod = method.Trim();
        ReportedReference = string.IsNullOrWhiteSpace(reference) ? null : reference.Trim();
        // Si venía de un Reject previo (Note seteado), limpiamos para
        // no confundir.
        Note = null;
        RejectedAt = null;
        UpdatedAt = utcNow;
    }

    /// <summary>
    /// Paso 2: el SuperAdmin verificó el pago en el extracto bancario
    /// y lo aprueba. La factura pasa a Paid y queda lista para que
    /// el handler active/renueve la suscripción.
    /// </summary>
    public void Validate(Guid validatedByUserId, DateTime utcNow)
    {
        if (Status != SubscriptionInvoiceStatus.Reported)
            throw new DomainException(
                "Solo se puede validar una factura reportada.");
        if (validatedByUserId == Guid.Empty)
            throw new DomainException("El validador es obligatorio.");

        Status = SubscriptionInvoiceStatus.Paid;
        PaidAt = utcNow;
        // Snapshot final: el método/referencia validados son los reportados.
        PaymentMethod = ReportedMethod;
        Reference = ReportedReference;
        ValidatedByUserId = validatedByUserId;
        ValidatedAt = utcNow;
        UpdatedAt = utcNow;
    }

    /// <summary>
    /// Alternativa al flujo Report+Validate: el SuperAdmin marca paga
    /// directamente (caso "el salón pagó offline, yo lo registro").
    /// Salta el paso de Reported.
    /// </summary>
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

    /// <summary>
    /// Paso 2 alternativo: el SuperAdmin rechaza el pago reportado
    /// (no encontró la transferencia en el banco). La factura vuelve
    /// a Pending para que el salón pueda re-reportar.
    /// La razón se guarda en Note.
    /// </summary>
    public void Reject(string reason, DateTime utcNow)
    {
        if (Status != SubscriptionInvoiceStatus.Reported)
            throw new DomainException(
                "Solo se puede rechazar una factura reportada.");
        if (string.IsNullOrWhiteSpace(reason))
            throw new DomainException("La razón del rechazo es obligatoria.");

        Status = SubscriptionInvoiceStatus.Pending;
        RejectedAt = utcNow;
        Note = reason.Trim();
        UpdatedAt = utcNow;
        // ReportedAt/Method/Reference se preservan para que el salón
        // vea qué reportó y entienda qué corregir.
    }

    public void MarkFailed(string reason, DateTime utcNow)
    {
        if (string.IsNullOrWhiteSpace(reason))
            throw new DomainException("La razón del fallo es obligatoria.");
        Status = SubscriptionInvoiceStatus.Failed;
        Note = reason.Trim();
        UpdatedAt = utcNow;
    }

    /// <summary>
    /// Actualiza plan y monto de una factura Pending. Usado cuando el
    /// salón cambia de plan ANTES de pagar la factura emitida — para que
    /// la cobranza siga al plan vigente. Solo válido en Pending; si ya
    /// fue Reported/Paid/etc., el cambio de plan tiene que respetarla
    /// (la valida o rechaza el SuperAdmin antes que María pueda cambiar).
    /// </summary>
    public void UpdatePlanInfo(string newPlanCode, Money newAmount, DateTime utcNow)
    {
        if (Status != SubscriptionInvoiceStatus.Pending)
            throw new DomainException(
                "Solo se puede actualizar plan en facturas pendientes (sin reportar).");
        if (string.IsNullOrWhiteSpace(newPlanCode))
            throw new DomainException("PlanCode es obligatorio.");
        if (newAmount.Amount <= 0m)
            throw new DomainException("El monto debe ser mayor a cero.");

        PlanCode = newPlanCode.Trim().ToLowerInvariant();
        Amount = newAmount;
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
