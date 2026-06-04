using BellaSync.Domain.Common;
using BellaSync.Domain.ValueObjects;

namespace BellaSync.Domain.Entities;

/// <summary>
/// Pago recibido por una cita atendida. Representa **dinero que ya
/// entró** al salón — no un cobro pendiente ni una promesa.
///
/// Diferente de PaymentVoucher:
///   - PaymentVoucher = comprobante online ANTES de la cita (anticipo),
///     que la recepcionista valida visualmente.
///   - Payment        = pago FINAL recibido en sitio cuando la cita
///     termina. El medio puede ser efectivo, transferencia (Nequi,
///     Bancolombia, etc.) o tarjeta en datáfono físico. BellaSync NO
///     procesa el pago — solo lo registra para reportería.
///
/// Una cita puede tener N pagos:
///   - Caso típico: 1 pago = todo el servicio.
///   - Caso anticipo: cliente pagó $50k online (voucher) y al terminar
///     paga el saldo $110k en efectivo → 1 voucher + 1 payment.
///   - Caso split: cliente paga la mitad con tarjeta y la mitad en
///     efectivo → 2 payments.
///
/// Setters privados — toda mutación pasa por métodos verbales.
/// </summary>
public class Payment : BaseEntity, ITenantEntity
{
    private Payment() { }

    /// <summary>
    /// Factory: crea un pago nuevo. Valida invariantes básicas.
    ///
    /// `tip` es opcional (Money.Zero por default). La referencia es
    /// obligatoria para métodos digitales (Bancolombia/Nequi/etc.) y
    /// número de voucher para tarjeta — no la enforzamos a nivel
    /// dominio para flexibilidad, pero el handler de Application
    /// debería validarlo.
    /// </summary>
    public static Payment Create(
        Guid tenantId,
        Guid appointmentId,
        PaymentMethod method,
        string? provider,
        Money amount,
        Money? tip,
        string? reference,
        Guid? registeredByUserId,
        DateTime utcNow)
    {
        if (appointmentId == Guid.Empty)
            throw new DomainException("AppointmentId es obligatorio.");
        if (amount.Amount <= 0m)
            throw new DomainException("El monto del pago debe ser mayor a cero.");
        // tip ya está validado >= 0 por Money.Create — solo chequeamos null

        var normalizedProvider = NormalizeOptional(provider);

        // Reglas por método: Transfer requiere banco/billetera; Cash no
        // admite provider (no tiene sentido); Card y Other lo permiten
        // opcional.
        if (method == PaymentMethod.Transfer && normalizedProvider is null)
            throw new DomainException("Para Transferencia hay que indicar el banco o billetera.");
        if (method == PaymentMethod.Cash && normalizedProvider is not null)
            throw new DomainException("Efectivo no lleva proveedor.");

        var payment = new Payment { TenantId = tenantId };
        payment.AppointmentId = appointmentId;
        payment.Method = method;
        payment.Provider = normalizedProvider;
        payment.Amount = amount;
        payment.Tip = tip ?? Money.Zero;
        payment.Reference = NormalizeOptional(reference);
        payment.RegisteredByUserId = registeredByUserId;
        payment.RegisteredAt = utcNow;
        return payment;
    }

    /// <summary>Plumbing multi-tenant.</summary>
    public Guid TenantId { get; set; }

    public Guid AppointmentId { get; private set; }
    public Appointment? Appointment { get; private set; }

    /// <summary>Método de pago (Efectivo, Transferencia, Tarjeta, Otro).</summary>
    public PaymentMethod Method { get; private set; }

    /// <summary>
    /// Proveedor específico dentro del método:
    ///   - Transfer → "Bancolombia", "Nequi", "Daviplata", "Davivienda", "BBVA"…
    ///   - Card     → "Visa", "Mastercard", "AmEx", "Diners" (opcional)
    ///   - Cash     → null siempre
    ///   - Other    → descripción libre (opcional)
    /// Permite cruzar contra el extracto del banco correspondiente al
    /// final del día sin tener que parsear `Reference`.
    /// </summary>
    public string? Provider { get; private set; }

    /// <summary>
    /// Monto principal del pago (sin contar la propina). Money es el VO
    /// que asegura non-negativo y precisión adecuada para COP.
    /// </summary>
    public Money Amount { get; private set; } = Money.Zero;

    /// <summary>
    /// Propina opcional. Se trackea aparte para reportería
    /// (la propina típicamente no es ingreso del salón sino del estilista).
    /// </summary>
    public Money Tip { get; private set; } = Money.Zero;

    /// <summary>
    /// Número de referencia que la recepcionista anota:
    ///   - Efectivo: null (no aplica).
    ///   - Transferencia: número de aprobación / referencia que muestra
    ///     la app de Nequi/Bancolombia ("TRF-892341").
    ///   - Tarjeta: número del voucher impreso por el datáfono.
    ///   - Other: descripción libre del método.
    /// Sirve para reconciliar con el extracto bancario al final del día.
    /// </summary>
    public string? Reference { get; private set; }

    /// <summary>
    /// Quién registró el pago. null si fue un proceso automático
    /// (ej: validación de voucher genera un payment). Hoy siempre
    /// es el User loggeado de la recepción.
    /// </summary>
    public Guid? RegisteredByUserId { get; private set; }

    /// <summary>Cuándo se registró (UTC).</summary>
    public DateTime RegisteredAt { get; private set; }

    // ===== MÉTODOS VERBALES =====

    /// <summary>
    /// Total = Amount + Tip. Conveniencia para reportes.
    /// </summary>
    public Money Total() => Money.Create(Amount.Amount + Tip.Amount);

    /// <summary>
    /// Actualiza la referencia (típico: la recepcionista la dejó vacía y
    /// luego la copia del voucher impreso). No permite cambiar método ni
    /// monto — para eso, anular y crear de nuevo.
    /// </summary>
    public void UpdateReference(string? reference)
    {
        Reference = NormalizeOptional(reference);
    }

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
