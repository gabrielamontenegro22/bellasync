using BellaSync.Domain.Common;
using BellaSync.Domain.ValueObjects;

namespace BellaSync.Domain.Entities;

/// <summary>
/// Cita agendada. Raíz del agregado central del producto.
///
/// Una cita encapsula:
///   - El triángulo Customer + Stylist + Service para un slot temporal
///     [StartAt, EndAt].
///   - El precio snapshot (Money) — copia del precio del servicio al
///     momento de agendar, para que cambios futuros en el catálogo no
///     afecten citas históricas.
///   - El estado del ciclo de vida (Status) y del anticipo (DepositStatus).
///   - Un "hold" temporal cuando requiere anticipo: HoldExpiresAt indica
///     hasta cuándo el cupo está reservado sin pago confirmado.
///
/// Setters privados — toda mutación pasa por métodos verbales que validan
/// las transiciones de estado.
///
/// El cálculo de slots/overlap NO vive acá (es lógica de Application porque
/// requiere acceso al DbContext para ver otras citas del stylist).
/// </summary>
public class Appointment : BaseEntity, ITenantEntity
{
    private Appointment() { }

    /// <summary>
    /// Factory: crea una cita nueva. Valida invariantes básicas (tiempos,
    /// referencias no vacías, monto consistente con deposit).
    ///
    /// Calcula automáticamente:
    /// - Status: Pending si requiere deposit, Confirmed si no
    /// - DepositStatus: AwaitingPayment / NotRequired
    /// - HoldExpiresAt: min(creation + holdHours, StartAt - holdMinBeforeAppointment)
    /// </summary>
    public static Appointment Create(
        Guid tenantId,
        Guid customerId,
        Guid stylistId,
        Guid serviceId,
        DateTime startAtUtc,
        DateTime endAtUtc,
        Money priceSnapshot,
        Percentage depositPercentage,
        bool requiresDeposit,
        AppointmentChannel channel,
        string? notes,
        DateTime utcNow,
        TimeSpan holdDuration,
        TimeSpan holdMinBeforeAppointment)
    {
        if (customerId == Guid.Empty)
            throw new DomainException("CustomerId es obligatorio.");
        if (stylistId == Guid.Empty)
            throw new DomainException("StylistId es obligatorio.");
        if (serviceId == Guid.Empty)
            throw new DomainException("ServiceId es obligatorio.");
        if (endAtUtc <= startAtUtc)
            throw new DomainException("La hora de fin debe ser posterior a la de inicio.");
        if (startAtUtc <= utcNow)
            throw new DomainException("No se puede agendar una cita en el pasado.");

        if (requiresDeposit && depositPercentage.Value <= 0m)
            throw new DomainException("Si la cita requiere anticipo, el porcentaje debe ser > 0.");
        if (!requiresDeposit && depositPercentage.Value > 0m)
            throw new DomainException("Si la cita no requiere anticipo, el porcentaje debe ser 0.");

        var appointment = new Appointment
        {
            TenantId = tenantId,
        };
        appointment.CustomerId = customerId;
        appointment.StylistId = stylistId;
        appointment.ServiceId = serviceId;
        appointment.StartAt = startAtUtc;
        appointment.EndAt = endAtUtc;
        appointment.PriceSnapshot = priceSnapshot;
        appointment.DepositPercentage = depositPercentage;
        appointment.Channel = channel;
        appointment.Notes = NormalizeNotes(notes);

        if (requiresDeposit)
        {
            appointment.DepositAmount = depositPercentage.ApplyTo(priceSnapshot);
            appointment.DepositStatus = AppointmentDepositStatus.AwaitingPayment;
            appointment.Status = AppointmentStatus.Pending;

            // Hold = min(now + holdDuration, StartAt - holdMinBefore).
            // Garantiza que el cupo se libere antes de la cita si nadie pagó.
            var byDuration = utcNow.Add(holdDuration);
            var byProximity = startAtUtc.Subtract(holdMinBeforeAppointment);
            appointment.HoldExpiresAt = byDuration < byProximity ? byDuration : byProximity;
        }
        else
        {
            appointment.DepositAmount = Money.Zero;
            appointment.DepositStatus = AppointmentDepositStatus.NotRequired;
            // Sin anticipo, la cita queda confirmada de una.
            appointment.Status = AppointmentStatus.Confirmed;
            appointment.HoldExpiresAt = null;
        }

        return appointment;
    }

    // ===== PROPIEDADES =====

    /// <summary>Plumbing multi-tenant (set requerido por ITenantEntity).</summary>
    public Guid TenantId { get; set; }

    public Guid CustomerId { get; private set; }
    public Customer? Customer { get; private set; }

    public Guid StylistId { get; private set; }
    public Stylist? Stylist { get; private set; }

    public Guid ServiceId { get; private set; }
    public Service? Service { get; private set; }

    /// <summary>Inicio de la cita en UTC.</summary>
    public DateTime StartAt { get; private set; }

    /// <summary>Fin de la cita en UTC (StartAt + service.DurationMinutes).</summary>
    public DateTime EndAt { get; private set; }

    /// <summary>
    /// Precio total snapshot al momento de agendar (Money). Si el servicio
    /// cambia de precio en el catálogo, esta cita preserva el precio original.
    /// </summary>
    public Money PriceSnapshot { get; private set; } = Money.Zero;

    /// <summary>Porcentaje de anticipo aplicado (snapshot del Service.DepositPercentage).</summary>
    public Percentage DepositPercentage { get; private set; } = Percentage.Zero;

    /// <summary>Monto del anticipo (calculado de PriceSnapshot × DepositPercentage).</summary>
    public Money DepositAmount { get; private set; } = Money.Zero;

    public AppointmentStatus Status { get; private set; } = AppointmentStatus.Pending;
    public AppointmentDepositStatus DepositStatus { get; private set; } = AppointmentDepositStatus.NotRequired;

    public AppointmentChannel Channel { get; private set; }

    /// <summary>
    /// Hasta cuándo el cupo está reservado sin pago confirmado.
    /// Solo se setea si DepositStatus == AwaitingPayment.
    /// El background job ReleaseExpiredHolds cancela las citas con
    /// HoldExpiresAt &lt; now.
    /// </summary>
    public DateTime? HoldExpiresAt { get; private set; }

    /// <summary>Notas internas de la cita (alergias, fórmulas, preferencias).</summary>
    public string? Notes { get; private set; }

    public DateTime? CancelledAt { get; private set; }
    public string? CancellationReason { get; private set; }
    /// <summary>
    /// User que canceló la cita. Null para cancelaciones automáticas
    /// (ej: rechazo de voucher cancela la cita en background) o para
    /// cancelaciones viejas anteriores a este campo.
    /// </summary>
    public Guid? CancelledByUserId { get; private set; }
    public User? CancelledByUser { get; private set; }

    public DateTime? StartedAt { get; private set; }
    public DateTime? CompletedAt { get; private set; }

    // ===== MÉTODOS VERBALES (máquina de estados) =====

    /// <summary>
    /// Marca el anticipo como validado (la recepción aprobó el voucher).
    /// Permite que Confirm() proceda. Solo legal desde AwaitingPayment.
    /// </summary>
    public void ValidateDeposit()
    {
        if (DepositStatus != AppointmentDepositStatus.AwaitingPayment)
            throw new DomainException(
                $"No se puede validar el anticipo: estado actual {DepositStatus}.");
        DepositStatus = AppointmentDepositStatus.Validated;
    }

    /// <summary>
    /// Confirma la cita. Legal desde:
    /// - Pending sin anticipo requerido (raro, normalmente Create ya la deja Confirmed)
    /// - Pending con DepositStatus = Validated
    /// Libera el hold (HoldExpiresAt = null) porque el cupo queda firme.
    /// </summary>
    public void Confirm()
    {
        if (Status != AppointmentStatus.Pending)
            throw new DomainException(
                $"No se puede confirmar una cita en estado {Status}.");
        if (DepositStatus == AppointmentDepositStatus.AwaitingPayment)
            throw new DomainException(
                "No se puede confirmar: el anticipo aún no fue validado.");

        Status = AppointmentStatus.Confirmed;
        HoldExpiresAt = null;
    }

    /// <summary>
    /// Marca como en curso. Legal solo desde Confirmed.
    /// </summary>
    public void MarkInProgress(DateTime utcNow)
    {
        if (Status != AppointmentStatus.Confirmed)
            throw new DomainException(
                $"No se puede iniciar una cita en estado {Status}.");
        Status = AppointmentStatus.InProgress;
        StartedAt = utcNow;
    }

    /// <summary>
    /// Finaliza la cita. Legal solo desde InProgress.
    /// </summary>
    public void Complete(DateTime utcNow)
    {
        if (Status != AppointmentStatus.InProgress)
            throw new DomainException(
                $"No se puede completar una cita en estado {Status}.");
        Status = AppointmentStatus.Completed;
        CompletedAt = utcNow;
    }

    /// <summary>
    /// Marca al cliente como no asistente. Legal desde Confirmed o Pending.
    /// </summary>
    public void MarkNoShow()
    {
        if (Status != AppointmentStatus.Confirmed && Status != AppointmentStatus.Pending)
            throw new DomainException(
                $"No se puede marcar no-show en estado {Status}.");
        Status = AppointmentStatus.NoShow;
    }

    /// <summary>
    /// Cancela la cita. Legal desde Pending o Confirmed (no desde estados terminales
    /// ni desde InProgress — para evitar cancelar lo que ya está ocurriendo).
    /// Idempotente: si ya está cancelada, no pisa CancelledAt ni la razón.
    ///
    /// `cancelledByUserId` es opcional: en cancelaciones automáticas
    /// (background, rechazo de voucher) viene null y queda como "Sistema"
    /// en la UI. En cancelaciones desde la app, viene el UserId loggeado.
    /// </summary>
    public void Cancel(DateTime utcNow, string? reason = null, Guid? cancelledByUserId = null)
    {
        if (Status == AppointmentStatus.Cancelled) return;
        if (Status != AppointmentStatus.Pending && Status != AppointmentStatus.Confirmed)
            throw new DomainException(
                $"No se puede cancelar una cita en estado {Status}.");

        Status = AppointmentStatus.Cancelled;
        CancelledAt = utcNow;
        CancellationReason = NormalizeNotes(reason);
        CancelledByUserId = cancelledByUserId;
        HoldExpiresAt = null;
    }

    /// <summary>
    /// Reagenda la cita a un nuevo horario. La duración del servicio se
    /// preserva (EndAt = newStartAt + DurationOriginal).
    ///
    /// Reglas:
    ///  - Solo Pending y Confirmed pueden reagendarse. InProgress / Completed /
    ///    Cancelled / NoShow son estados terminales o en curso y no.
    ///  - La nueva fecha debe ser estrictamente posterior a `utcNow`.
    ///  - El hold (si aplica) se recalcula con la nueva proximidad: si la
    ///    cita aún está esperando pago y el nuevo slot está más cerca que
    ///    el hold actual, el hold se acorta.
    ///
    /// La validación de overlap NO vive acá — requiere acceso al DbContext
    /// para conocer las otras citas del stylist. La hace el handler de
    /// Application antes de llamar este método.
    /// </summary>
    public void Reschedule(
        DateTime newStartAtUtc,
        DateTime utcNow,
        TimeSpan holdDuration,
        TimeSpan holdMinBeforeAppointment)
    {
        if (Status != AppointmentStatus.Pending && Status != AppointmentStatus.Confirmed)
            throw new DomainException(
                $"No se puede reagendar una cita en estado {Status}.");

        if (newStartAtUtc <= utcNow)
            throw new DomainException("La nueva hora debe ser posterior a ahora.");

        var duration = EndAt - StartAt;
        StartAt = newStartAtUtc;
        EndAt = newStartAtUtc.Add(duration);

        // Recalcular hold si la cita aún espera anticipo. Mantenemos la
        // misma lógica que el factory: min(now + holdDuration, newStart - holdMinBefore).
        if (DepositStatus == AppointmentDepositStatus.AwaitingPayment)
        {
            var byDuration = utcNow.Add(holdDuration);
            var byProximity = newStartAtUtc.Subtract(holdMinBeforeAppointment);
            HoldExpiresAt = byDuration < byProximity ? byDuration : byProximity;
        }
    }

    /// <summary>
    /// True si las dos citas se solapan en tiempo (intervalos cerrados-abiertos
    /// [start, end): permite que una cita termine exactamente cuando empieza la siguiente).
    /// </summary>
    public bool OverlapsWith(DateTime otherStart, DateTime otherEnd) =>
        StartAt < otherEnd && otherStart < EndAt;

    /// <summary>
    /// True si el hold venció (cita Pending sin pago, debería cancelarse).
    /// </summary>
    public bool IsHoldExpired(DateTime utcNow) =>
        HoldExpiresAt is not null && HoldExpiresAt < utcNow;

    /// <summary>Actualiza notas. Setter privado vía método verbal.</summary>
    public void UpdateNotes(string? notes) => Notes = NormalizeNotes(notes);

    private static string? NormalizeNotes(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
