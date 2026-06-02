using BellaSync.Domain.Common;
using BellaSync.Domain.ValueObjects;

namespace BellaSync.Domain.Entities;

/// <summary>
/// Comprobante de pago recibido por el cliente (por WhatsApp típicamente).
/// Vincula a un Appointment y queda en cola para validación por recepción.
///
/// Implementa ITenantEntity para que el filtro global multi-tenant funcione
/// (la cola de validación es por salón).
///
/// Setters privados — solo se muta vía Confirm/Reject/RequestClarification.
/// </summary>
public class PaymentVoucher : BaseEntity, ITenantEntity
{
    private PaymentVoucher() { }

    /// <summary>
    /// Factory: crea un voucher nuevo, en estado Pending por defecto.
    /// El monto reportado por el cliente es opcional (algunos solo mandan foto).
    /// </summary>
    public static PaymentVoucher Create(
        Guid tenantId,
        Guid appointmentId,
        Money reportedAmount,
        string? bank,
        string? referenceNumber,
        string? senderName,
        string? senderPhone,
        string? imageUrl,
        DateTime utcNow)
    {
        if (appointmentId == Guid.Empty)
            throw new DomainException("AppointmentId es obligatorio.");

        var voucher = new PaymentVoucher
        {
            TenantId = tenantId,
        };
        voucher.AppointmentId = appointmentId;
        voucher.ReportedAmount = reportedAmount;
        voucher.Bank = Normalize(bank);
        voucher.ReferenceNumber = Normalize(referenceNumber);
        voucher.SenderName = Normalize(senderName);
        voucher.SenderPhone = Normalize(senderPhone);
        voucher.ImageUrl = Normalize(imageUrl);
        voucher.ReceivedAt = utcNow;
        voucher.Status = PaymentVoucherStatus.Pending;
        return voucher;
    }

    public Guid TenantId { get; set; }

    public Guid AppointmentId { get; private set; }
    public Appointment? Appointment { get; private set; }

    /// <summary>Monto que el cliente reporta haber transferido (snapshot).</summary>
    public Money ReportedAmount { get; private set; } = Money.Zero;

    public string? Bank { get; private set; }
    public string? ReferenceNumber { get; private set; }
    public string? SenderName { get; private set; }
    public string? SenderPhone { get; private set; }

    /// <summary>URL de la imagen del comprobante (S3, blob storage, etc.). Opcional.</summary>
    public string? ImageUrl { get; private set; }

    /// <summary>Cuándo el cliente envió el comprobante (no cuando se creó el row).</summary>
    public DateTime ReceivedAt { get; private set; }

    public PaymentVoucherStatus Status { get; private set; } = PaymentVoucherStatus.Pending;

    public DateTime? DecidedAt { get; private set; }
    public Guid? DecidedBy { get; private set; }   // UserId del que validó
    public string? DecisionNotes { get; private set; }

    // ===== MÉTODOS VERBALES =====

    /// <summary>
    /// Marca el voucher como validado. Solo legal desde Pending.
    /// La cita asociada se confirma por separado (ver ValidateVoucherHandler).
    /// </summary>
    public void Confirm(Guid decidedBy, DateTime utcNow, string? notes = null)
    {
        if (Status != PaymentVoucherStatus.Pending)
            throw new DomainException($"No se puede confirmar un voucher en estado {Status}.");
        Status = PaymentVoucherStatus.Validated;
        DecidedAt = utcNow;
        DecidedBy = decidedBy;
        DecisionNotes = Normalize(notes);
    }

    public void Reject(Guid decidedBy, DateTime utcNow, string? notes = null)
    {
        if (Status != PaymentVoucherStatus.Pending)
            throw new DomainException($"No se puede rechazar un voucher en estado {Status}.");
        Status = PaymentVoucherStatus.Rejected;
        DecidedAt = utcNow;
        DecidedBy = decidedBy;
        DecisionNotes = Normalize(notes);
    }

    public void RequestClarification(Guid decidedBy, DateTime utcNow, string? notes = null)
    {
        if (Status != PaymentVoucherStatus.Pending)
            throw new DomainException($"No se puede pedir aclaración de un voucher en estado {Status}.");
        Status = PaymentVoucherStatus.NeedsClarification;
        DecidedAt = utcNow;
        DecidedBy = decidedBy;
        DecisionNotes = Normalize(notes);
    }

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
