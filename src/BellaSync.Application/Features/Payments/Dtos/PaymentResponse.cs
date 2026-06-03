namespace BellaSync.Application.Features.Payments.Dtos;

/// <summary>
/// DTO de salida de un pago registrado. Plano (sin objetos anidados)
/// porque el frontend pinta tablas/listas simples.
/// </summary>
public class PaymentResponse
{
    public Guid Id { get; set; }
    public Guid AppointmentId { get; set; }

    /// <summary>"Cash" / "Bancolombia" / "Nequi" / "Daviplata" / "CreditCard" / "DebitCard" / "Other"</summary>
    public string Method { get; set; } = string.Empty;

    public decimal Amount { get; set; }
    public decimal Tip { get; set; }
    public decimal Total { get; set; }   // Amount + Tip, conveniencia para el UI

    public string? Reference { get; set; }
    public Guid? RegisteredByUserId { get; set; }
    public DateTime RegisteredAt { get; set; }

    // Snapshot mínimo del contexto de la cita — útil para tabla del CRM
    // donde quieres mostrar "qué servicio se pagó" sin tener que hacer
    // otra request por cada fila.
    public string ServiceName { get; set; } = string.Empty;
    public string StylistName { get; set; } = string.Empty;
    public DateTime AppointmentStartAt { get; set; }
}
