namespace BellaSync.Application.Features.SaasAdmin.Subscriptions.Dtos;

/// <summary>
/// Fila de la cola de validación que ve el SuperAdmin. Snapshot
/// suficiente para decidir validar o rechazar sin entrar a la
/// factura — banco/referencia/monto/salón.
/// </summary>
public sealed class PendingValidationRow
{
    public Guid InvoiceId { get; init; }
    public Guid TenantId { get; init; }
    public string TenantName { get; init; } = string.Empty;
    public string TenantSlug { get; init; } = string.Empty;

    public string PlanCode { get; init; } = string.Empty;
    public string PlanName { get; init; } = string.Empty;
    public decimal Amount { get; init; }

    public DateTime IssuedAt { get; init; }
    public DateTime DueDate { get; init; }
    public DateTime ReportedAt { get; init; }
    public string ReportedMethod { get; init; } = string.Empty;
    public string? ReportedReference { get; init; }

    public DateTime PeriodStart { get; init; }
    public DateTime PeriodEnd { get; init; }
}
