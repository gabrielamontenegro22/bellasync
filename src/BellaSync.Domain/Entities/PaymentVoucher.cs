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
    /// <summary>Nav property al user que validó/rechazó (para mostrar nombre en cola).</summary>
    public User? DecidedByUser { get; private set; }
    public string? DecisionNotes { get; private set; }

    // ===== TRACKING DE REFUND CUANDO LA CITA SE CANCELA =====
    // Estos campos quedan null mientras el voucher está activo. Se llenan
    // SOLO cuando la cita asociada se cancela y el voucher estaba Validated.

    /// <summary>
    /// Qué pasó con el anticipo cuando se canceló la cita. Null si la
    /// cita sigue activa o el voucher no estaba Validated al cancelar.
    ///
    /// - Refunded:      admin va a transferir la plata de vuelta.
    /// - CreditPending: cliente reagenda, anticipo aplica a nueva cita.
    /// - Forfeited:     cancelación tardía → salón se queda con la plata.
    /// </summary>
    public DepositRefundDecision? RefundDecision { get; private set; }

    /// <summary>
    /// Cuándo se finalizó la acción del refund. Para Refunded =
    /// cuándo la admin marcó la transferencia como hecha. Para
    /// CreditPending = cuándo se aplicó a la nueva cita. Para
    /// Forfeited = mismo momento que RefundDecision (no requiere acción).
    /// </summary>
    public DateTime? RefundResolvedAt { get; private set; }

    /// <summary>Usuario que finalizó la acción del refund (FK a User).</summary>
    public Guid? RefundResolvedByUserId { get; private set; }

    /// <summary>
    /// Cuánto del ReportedAmount ya se aplicó como crédito a citas nuevas.
    /// Default 0. Se incrementa cuando la admin aplica el crédito a una
    /// cita futura desde NewAppointmentModal. El crédito disponible es:
    ///
    ///   <c>availableCredit = ReportedAmount - AmountApplied</c>
    ///
    /// Solo tiene sentido cuando RefundDecision = CreditPending. El campo
    /// permite aplicar el crédito EN PARTES — si el cliente paga $90k de
    /// anticipo y cancela, puede usar $45k en una cita corta y los $45k
    /// restantes seguir disponibles para otra cita futura.
    ///
    /// Cuando AmountApplied alcanza ReportedAmount, el voucher se marca
    /// como resuelto (RefundResolvedAt) automáticamente.
    /// </summary>
    public decimal AmountApplied { get; private set; }

    /// <summary>
    /// Crédito disponible para aplicar a una nueva cita. Solo es positivo
    /// cuando el voucher está validado, marcado como CreditPending y aún
    /// no se aplicó todo. Helper para queries y validaciones.
    /// </summary>
    public decimal AvailableCredit => RefundDecision == DepositRefundDecision.CreditPending
        ? ReportedAmount.Amount - AmountApplied
        : 0m;

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

    /// <summary>
    /// Llamado por el handler de CancelAppointment cuando el voucher
    /// estaba Validated. Registra la decisión sobre el anticipo:
    ///   - Forfeited: queda resuelto inmediato (salón se quedó la plata).
    ///   - Refunded:  queda pendiente hasta que admin marque "transferido".
    ///   - CreditPending: queda pendiente hasta que la cliente cree una
    ///     nueva cita y consuma el crédito.
    /// </summary>
    public void RecordRefundDecision(DepositRefundDecision decision, DateTime utcNow, Guid decidedBy)
    {
        if (Status != PaymentVoucherStatus.Validated)
            throw new DomainException(
                "Solo se puede registrar decisión de refund en vouchers Validados " +
                $"(este está en {Status}).");
        if (RefundDecision is not null)
            throw new DomainException("Este voucher ya tiene decisión de refund registrada.");

        RefundDecision = decision;
        // Forfeited es la única decisión que se resuelve sola — no hay
        // acción posterior. Las otras dos quedan pendientes.
        if (decision == DepositRefundDecision.Forfeited)
        {
            RefundResolvedAt = utcNow;
            RefundResolvedByUserId = decidedBy;
        }
    }

    /// <summary>
    /// La admin marca la transferencia de devolución como realizada
    /// (típico flow: ve la "pendiente" en Caja, hace la transferencia
    /// bancaria por fuera, vuelve y marca acá). Solo aplica a Refunded.
    /// </summary>
    public void MarkRefundResolved(DateTime utcNow, Guid resolvedBy)
    {
        if (RefundDecision is null)
            throw new DomainException("Este voucher no tiene decisión de refund pendiente.");
        if (RefundResolvedAt is not null)
            throw new DomainException("Este refund ya fue marcado como resuelto.");
        RefundResolvedAt = utcNow;
        RefundResolvedByUserId = resolvedBy;
    }

    /// <summary>
    /// Aplica una porción del crédito disponible a una nueva cita. Solo
    /// legal para vouchers Validated con RefundDecision = CreditPending
    /// y todavía con saldo disponible.
    ///
    /// El handler de Application crea un voucher NUEVO en la cita de
    /// destino (status Validated, sin RefundDecision) por el monto
    /// aplicado — este método solo trackea el consumo del crédito en
    /// el voucher original.
    ///
    /// Si el monto aplicado iguala el saldo total, el voucher original
    /// se cierra automáticamente (RefundResolvedAt). Si queda saldo,
    /// el voucher sigue apareciendo como crédito disponible para
    /// futuras aplicaciones (saldo parcial).
    /// </summary>
    public void ApplyCredit(decimal amountToApply, DateTime utcNow, Guid appliedBy)
    {
        if (Status != PaymentVoucherStatus.Validated)
            throw new DomainException(
                $"Solo se puede aplicar crédito de vouchers Validated (este está en {Status}).");
        if (RefundDecision != DepositRefundDecision.CreditPending)
            throw new DomainException(
                "Solo se puede aplicar crédito de vouchers marcados como CreditPending.");
        if (RefundResolvedAt is not null)
            throw new DomainException("Este crédito ya fue cerrado.");
        if (amountToApply <= 0m)
            throw new DomainException("El monto a aplicar debe ser mayor a cero.");
        if (amountToApply > AvailableCredit)
            throw new DomainException(
                $"El monto a aplicar ({amountToApply:N0}) excede el crédito disponible ({AvailableCredit:N0}).");

        AmountApplied += amountToApply;

        // Si consumimos todo el saldo, cerramos el voucher.
        if (AmountApplied >= ReportedAmount.Amount)
        {
            RefundResolvedAt = utcNow;
            RefundResolvedByUserId = appliedBy;
        }
    }

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
