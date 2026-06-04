namespace BellaSync.Application.Features.Payments.Dtos;

/// <summary>
/// DTO de salida de un pago registrado. Plano (sin objetos anidados)
/// porque el frontend pinta tablas/listas simples.
/// </summary>
public class PaymentResponse
{
    public Guid Id { get; set; }
    public Guid AppointmentId { get; set; }

    /// <summary>"Cash" / "Transfer" / "Card" / "Other"</summary>
    public string Method { get; set; } = string.Empty;

    /// <summary>
    /// Banco/billetera ("Bancolombia", "Nequi") cuando Method=Transfer,
    /// o marca ("Visa") cuando Method=Card. Null para Cash.
    /// </summary>
    public string? Provider { get; set; }

    public decimal Amount { get; set; }
    public decimal Tip { get; set; }
    public decimal Total { get; set; }   // Amount + Tip, conveniencia para el UI

    public string? Reference { get; set; }
    public Guid? RegisteredByUserId { get; set; }
    public DateTime RegisteredAt { get; set; }

    // Snapshot mínimo del contexto de la cita — útil para tablas/listas
    // (CRM, cierre de caja) donde quieres mostrar quién pagó qué sin
    // hacer otra request por cada fila.
    public string CustomerName { get; set; } = string.Empty;
    public string ServiceName { get; set; } = string.Empty;
    public string StylistName { get; set; } = string.Empty;
    public DateTime AppointmentStartAt { get; set; }
}
