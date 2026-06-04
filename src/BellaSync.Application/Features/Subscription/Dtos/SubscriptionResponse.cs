namespace BellaSync.Application.Features.Subscription.Dtos;

/// <summary>
/// Snapshot completo de la suscripción del salón. Incluye plan actual,
/// fecha de próximo cobro, historial de invoices y catálogo de planes
/// disponibles para que el frontend pueda armar la pantalla con un solo
/// call.
/// </summary>
public sealed class SubscriptionResponse
{
    /// <summary>"basic" | "professional" | "premium".</summary>
    public string PlanCode { get; init; } = string.Empty;
    public string PlanName { get; init; } = string.Empty;
    public string PlanTagline { get; init; } = string.Empty;
    public decimal MonthlyPrice { get; init; }
    public IReadOnlyList<string> Features { get; init; } = new List<string>();

    /// <summary>"Trial" | "Active" | "PastDue" | "Cancelled".</summary>
    public string Status { get; init; } = string.Empty;

    public DateTime StartedAt { get; init; }
    public DateTime CurrentPeriodEnd { get; init; }
    public DateTime? TrialEndsAt { get; init; }
    public DateTime? CancelledAt { get; init; }

    /// <summary>Días restantes hasta el próximo cobro (negativo si vencido).</summary>
    public int DaysUntilNextCharge { get; init; }

    /// <summary>True si está en Trial y los días restantes &lt;= 3.</summary>
    public bool TrialEndingSoon { get; init; }

    /// <summary>Catálogo completo de planes — para el modal "Cambiar plan".</summary>
    public IReadOnlyList<PlanOption> AvailablePlans { get; init; } = new List<PlanOption>();

    /// <summary>Últimas N facturas para el historial.</summary>
    public IReadOnlyList<InvoiceRow> Invoices { get; init; } = new List<InvoiceRow>();

    /// <summary>Factura pendiente más cercana (para el botón "Pagar ahora").</summary>
    public InvoiceRow? NextDueInvoice { get; init; }

    /// <summary>
    /// Factura más reciente en estado Reported (pago reportado por la
    /// admin, pendiente de validación del SuperAdmin). Si existe, el
    /// frontend muestra el banner "Pago en validación".
    /// </summary>
    public InvoiceRow? PendingValidationInvoice { get; init; }

    /// <summary>
    /// Si la factura más reciente fue rechazada por el SuperAdmin, este
    /// campo trae la razón para mostrarla al salón. Se vacía cuando el
    /// salón vuelve a reportar.
    /// </summary>
    public string? LastRejectionReason { get; init; }
}

public sealed class PlanOption
{
    public string Code { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string Tagline { get; init; } = string.Empty;
    public decimal MonthlyPrice { get; init; }
    public IReadOnlyList<string> Features { get; init; } = new List<string>();
    public bool IsHighlighted { get; init; }
    /// <summary>True si es el plan actual del salón.</summary>
    public bool IsCurrent { get; init; }
}

public sealed class InvoiceRow
{
    public Guid Id { get; init; }
    public string PlanCode { get; init; } = string.Empty;
    public string PlanName { get; init; } = string.Empty;
    public decimal Amount { get; init; }
    public DateTime PeriodStart { get; init; }
    public DateTime PeriodEnd { get; init; }
    public DateTime DueDate { get; init; }
    public DateTime IssuedAt { get; init; }
    /// <summary>"Pending" | "Reported" | "Paid" | "Failed" | "Waived".</summary>
    public string Status { get; init; } = string.Empty;
    public DateTime? PaidAt { get; init; }
    public string? PaymentMethod { get; init; }
    public string? Reference { get; init; }
    public string? Note { get; init; }

    // Reporte (paso intermedio anti-pasarela)
    public DateTime? ReportedAt { get; init; }
    public string? ReportedMethod { get; init; }
    public string? ReportedReference { get; init; }
    public DateTime? RejectedAt { get; init; }
}
