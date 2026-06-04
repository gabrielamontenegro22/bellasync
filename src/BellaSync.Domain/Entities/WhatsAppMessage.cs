using BellaSync.Domain.Common;

namespace BellaSync.Domain.Entities;

/// <summary>
/// Un mensaje WhatsApp concreto, encolado y opcionalmente despachado.
///
/// Ciclo de vida:
///   1. El dispatcher detecta un evento (cita creada, cita en 24h, etc.)
///   2. Renderiza la plantilla → crea WhatsAppMessage con Status=Queued
///   3. En el próximo tick, el dispatcher levanta los Queued y los manda
///      vía IWhatsAppSender (NoOp/Twilio/Meta)
///   4. Actualiza Status=Sent o Failed según el resultado
///
/// Por qué guardarlos en BD (vs solo loguear y olvidar):
///   - Trazabilidad: la admin puede ver el historial "¿qué le mandamos a
///     María Jose y cuándo?"
///   - Reintentos: un mensaje Failed se puede reenviar manualmente.
///   - Idempotencia: antes de crear un nuevo Reminder24h para una cita,
///     chequeamos que no exista ya uno Queued/Sent para esa misma cita.
///     Sin esto, en cada tick del dispatcher se duplicaría.
///   - Cancelación: si la cita se cancela y todavía está Queued,
///     marcamos el mensaje como Cancelled y el dispatcher lo salta.
///
/// Setters privados — toda mutación pasa por métodos verbales.
/// </summary>
public class WhatsAppMessage : BaseEntity, ITenantEntity
{
    private WhatsAppMessage() { }

    /// <summary>
    /// Factory: encola un mensaje. RenderedBody YA debe venir resuelto
    /// (sin placeholders) — el dispatcher hace el render antes de llamar.
    /// </summary>
    public static WhatsAppMessage Queue(
        Guid tenantId,
        WhatsAppTemplateKind kind,
        string customerPhone,
        string renderedBody,
        Guid? appointmentId,
        DateTime utcNow)
    {
        if (tenantId == Guid.Empty)
            throw new DomainException("TenantId es obligatorio.");
        if (string.IsNullOrWhiteSpace(customerPhone))
            throw new DomainException("El teléfono del cliente es obligatorio.");
        if (string.IsNullOrWhiteSpace(renderedBody))
            throw new DomainException("El cuerpo renderizado no puede estar vacío.");

        return new WhatsAppMessage
        {
            TenantId = tenantId,
            Kind = kind,
            CustomerPhone = customerPhone.Trim(),
            RenderedBody = renderedBody.Trim(),
            AppointmentId = appointmentId,
            Status = WhatsAppMessageStatus.Queued,
            QueuedAt = utcNow,
        };
    }

    public Guid TenantId { get; set; }

    public WhatsAppTemplateKind Kind { get; private set; }

    /// <summary>
    /// Teléfono del cliente al momento del encolado. NO se actualiza si
    /// el cliente cambia el suyo después — el mensaje está pensado para
    /// ESE número en ESE momento. Trazabilidad histórica.
    /// </summary>
    public string CustomerPhone { get; private set; } = string.Empty;

    /// <summary>Cuerpo del mensaje YA con placeholders reemplazados.</summary>
    public string RenderedBody { get; private set; } = string.Empty;

    /// <summary>
    /// Cita asociada (si aplica). Reminder24h, Ready2h y ConfirmCreated
    /// tienen appointment; Birthday y PendingDeposit pueden no tener uno
    /// específico — los recordatorios genéricos no tienen appointment.
    /// </summary>
    public Guid? AppointmentId { get; private set; }

    public WhatsAppMessageStatus Status { get; private set; }

    public DateTime QueuedAt { get; private set; }

    public DateTime? SentAt { get; private set; }

    public DateTime? FailedAt { get; private set; }

    /// <summary>
    /// Razón del fallo cuando Status=Failed. Lo que devolvió el adapter:
    /// "número inválido", "rate limit", "template no aprobado", etc.
    /// La admin lo ve en la UI para decidir si reintenta o pide al
    /// cliente que confirme su número.
    /// </summary>
    public string? FailureReason { get; private set; }

    /// <summary>
    /// Identificador que devuelve el adapter (Twilio MessageSid, Meta
    /// message id). Sirve para reconciliar webhooks de delivery/read
    /// receipts en el futuro. null mientras Status=Queued/Cancelled.
    /// </summary>
    public string? ExternalMessageId { get; private set; }

    // ===== MÉTODOS VERBALES =====

    /// <summary>
    /// Llamado por el dispatcher cuando el adapter aceptó el mensaje.
    /// </summary>
    public void MarkSent(string? externalMessageId, DateTime utcNow)
    {
        if (Status != WhatsAppMessageStatus.Queued)
            throw new DomainException($"Solo mensajes Queued pueden marcarse Sent; actual={Status}.");

        Status = WhatsAppMessageStatus.Sent;
        SentAt = utcNow;
        ExternalMessageId = externalMessageId;
    }

    /// <summary>
    /// Llamado por el dispatcher cuando el adapter rechazó. La razón
    /// queda visible para la admin.
    /// </summary>
    public void MarkFailed(string reason, DateTime utcNow)
    {
        if (Status != WhatsAppMessageStatus.Queued)
            throw new DomainException($"Solo mensajes Queued pueden marcarse Failed; actual={Status}.");
        if (string.IsNullOrWhiteSpace(reason))
            throw new DomainException("La razón del fallo es obligatoria.");

        Status = WhatsAppMessageStatus.Failed;
        FailedAt = utcNow;
        FailureReason = reason.Trim();
    }

    /// <summary>
    /// Llamado cuando la cita asociada se cancela antes de que el
    /// mensaje salga. Solo aplica a mensajes Queued.
    /// </summary>
    public void Cancel()
    {
        if (Status != WhatsAppMessageStatus.Queued) return;  // idempotente
        Status = WhatsAppMessageStatus.Cancelled;
    }

    /// <summary>
    /// Resetea un mensaje Failed a Queued para que el próximo tick del
    /// dispatcher vuelva a intentarlo. La admin lo dispara desde la UI.
    /// </summary>
    public void Retry(DateTime utcNow)
    {
        if (Status != WhatsAppMessageStatus.Failed)
            throw new DomainException($"Solo mensajes Failed pueden reintentarse; actual={Status}.");

        Status = WhatsAppMessageStatus.Queued;
        FailedAt = null;
        FailureReason = null;
        QueuedAt = utcNow;
    }
}
