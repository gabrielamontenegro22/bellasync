using BellaSync.Domain.Common;
using BellaSync.Domain.Entities;
using BellaSync.Domain.ValueObjects;
using FluentAssertions;

namespace BellaSync.Application.Tests.Domain;

/// <summary>
/// Tests unitarios sobre Appointment.cs (dominio puro, sin BD).
/// Verifican que las invariantes y la máquina de estados se enforzan
/// desde la entidad, no desde el handler.
/// </summary>
public class AppointmentTests
{
    private static readonly DateTime UtcNow = new(2026, 6, 5, 12, 0, 0, DateTimeKind.Utc);
    private static readonly TimeSpan Hold = TimeSpan.FromHours(3);
    private static readonly TimeSpan HoldMinBefore = TimeSpan.FromMinutes(30);

    private static Appointment NewAppointment(
        bool requiresDeposit = false,
        DateTime? startAt = null,
        DateTime? utcNow = null)
    {
        var start = startAt ?? UtcNow.AddHours(2);
        return Appointment.Create(
            tenantId: Guid.NewGuid(),
            customerId: Guid.NewGuid(),
            stylistId: Guid.NewGuid(),
            serviceId: Guid.NewGuid(),
            startAtUtc: start,
            endAtUtc: start.AddMinutes(60),
            priceSnapshot: Money.Create(100000),
            depositPercentage: requiresDeposit ? Percentage.Create(50) : Percentage.Zero,
            requiresDeposit: requiresDeposit,
            channel: AppointmentChannel.Reception,
            notes: null,
            utcNow: utcNow ?? UtcNow,
            holdDuration: Hold,
            holdMinBeforeAppointment: HoldMinBefore);
    }

    // ===== Factory =====

    [Fact]
    public void Create_without_deposit_is_immediately_confirmed()
    {
        var appt = NewAppointment(requiresDeposit: false);

        appt.Status.Should().Be(AppointmentStatus.Confirmed);
        appt.DepositStatus.Should().Be(AppointmentDepositStatus.NotRequired);
        appt.DepositAmount.Should().Be(Money.Zero);
        appt.HoldExpiresAt.Should().BeNull();
    }

    [Fact]
    public void Create_with_deposit_is_pending_awaiting_payment()
    {
        var appt = NewAppointment(requiresDeposit: true);

        appt.Status.Should().Be(AppointmentStatus.Pending);
        appt.DepositStatus.Should().Be(AppointmentDepositStatus.AwaitingPayment);
        appt.DepositAmount.Amount.Should().Be(50000m); // 50% de 100000
        appt.HoldExpiresAt.Should().NotBeNull();
    }

    [Fact]
    public void Create_hold_is_min_of_duration_and_appointment_minus_buffer()
    {
        // Cita en 1h. Hold de 3h. Buffer 30min antes cita = StartAt - 30min = now + 30min.
        // El min(now+3h, now+30min) = now+30min.
        var startAt = UtcNow.AddHours(1);
        var appt = NewAppointment(requiresDeposit: true, startAt: startAt);

        appt.HoldExpiresAt.Should().Be(startAt.AddMinutes(-30));
    }

    [Fact]
    public void Create_in_the_past_throws()
    {
        var action = () => NewAppointment(startAt: UtcNow.AddMinutes(-10));

        action.Should().Throw<DomainException>()
            .WithMessage("*pasado*");
    }

    [Fact]
    public void Create_with_inconsistent_deposit_throws()
    {
        var action = () => Appointment.Create(
            tenantId: Guid.NewGuid(),
            customerId: Guid.NewGuid(),
            stylistId: Guid.NewGuid(),
            serviceId: Guid.NewGuid(),
            startAtUtc: UtcNow.AddHours(2),
            endAtUtc: UtcNow.AddHours(3),
            priceSnapshot: Money.Create(100000),
            depositPercentage: Percentage.Create(50),   // dice 50%
            requiresDeposit: false,                      // pero no requiere
            channel: AppointmentChannel.Reception,
            notes: null,
            utcNow: UtcNow,
            holdDuration: Hold,
            holdMinBeforeAppointment: HoldMinBefore);

        action.Should().Throw<DomainException>();
    }

    // ===== Confirm =====

    [Fact]
    public void Confirm_from_pending_with_validated_deposit_succeeds()
    {
        var appt = NewAppointment(requiresDeposit: true);
        appt.ValidateDeposit();

        appt.Confirm();

        appt.Status.Should().Be(AppointmentStatus.Confirmed);
        appt.HoldExpiresAt.Should().BeNull();
    }

    [Fact]
    public void Confirm_from_pending_with_awaiting_payment_throws()
    {
        var appt = NewAppointment(requiresDeposit: true);

        var action = () => appt.Confirm();

        action.Should().Throw<DomainException>().WithMessage("*anticipo*");
    }

    [Fact]
    public void Confirm_from_already_confirmed_throws()
    {
        var appt = NewAppointment(requiresDeposit: false);  // ya está Confirmed

        var action = () => appt.Confirm();

        action.Should().Throw<DomainException>();
    }

    // ===== MarkInProgress / Complete =====

    [Fact]
    public void MarkInProgress_from_confirmed_succeeds()
    {
        var appt = NewAppointment(requiresDeposit: false);
        var startTime = UtcNow.AddMinutes(50);

        appt.MarkInProgress(startTime);

        appt.Status.Should().Be(AppointmentStatus.InProgress);
        appt.StartedAt.Should().Be(startTime);
    }

    [Fact]
    public void MarkInProgress_from_pending_throws()
    {
        var appt = NewAppointment(requiresDeposit: true);  // Pending

        var action = () => appt.MarkInProgress(UtcNow);

        action.Should().Throw<DomainException>();
    }

    [Fact]
    public void Complete_only_from_in_progress()
    {
        var appt = NewAppointment(requiresDeposit: false);

        // Desde Confirmed → fail
        var action1 = () => appt.Complete(UtcNow);
        action1.Should().Throw<DomainException>();

        // Después de MarkInProgress → OK
        appt.MarkInProgress(UtcNow);
        appt.Complete(UtcNow.AddMinutes(30));
        appt.Status.Should().Be(AppointmentStatus.Completed);
    }

    // ===== Cancel =====

    [Fact]
    public void Cancel_from_pending_succeeds()
    {
        var appt = NewAppointment(requiresDeposit: true);

        appt.Cancel(UtcNow, "test");

        appt.Status.Should().Be(AppointmentStatus.Cancelled);
        appt.CancellationReason.Should().Be("test");
        appt.HoldExpiresAt.Should().BeNull();
    }

    [Fact]
    public void Cancel_is_idempotent()
    {
        var appt = NewAppointment(requiresDeposit: false);
        appt.Cancel(UtcNow, "first");
        appt.Cancel(UtcNow.AddHours(1), "second");

        // Mantiene la primera fecha y razón
        appt.CancelledAt.Should().Be(UtcNow);
        appt.CancellationReason.Should().Be("first");
    }

    [Fact]
    public void Cancel_from_completed_throws()
    {
        var appt = NewAppointment(requiresDeposit: false);
        appt.MarkInProgress(UtcNow);
        appt.Complete(UtcNow.AddMinutes(30));

        var action = () => appt.Cancel(UtcNow.AddHours(1));

        action.Should().Throw<DomainException>();
    }

    // ===== NoShow =====

    [Fact]
    public void MarkNoShow_from_confirmed_or_pending_succeeds()
    {
        var confirmed = NewAppointment(requiresDeposit: false);
        confirmed.MarkNoShow();
        confirmed.Status.Should().Be(AppointmentStatus.NoShow);

        var pending = NewAppointment(requiresDeposit: true);
        pending.MarkNoShow();
        pending.Status.Should().Be(AppointmentStatus.NoShow);
    }

    [Fact]
    public void MarkNoShow_from_completed_throws()
    {
        var appt = NewAppointment(requiresDeposit: false);
        appt.MarkInProgress(UtcNow);
        appt.Complete(UtcNow.AddMinutes(30));

        var action = () => appt.MarkNoShow();
        action.Should().Throw<DomainException>();
    }

    // ===== OverlapsWith =====

    [Theory]
    [InlineData("14:00", "15:00", "14:30", "15:30", true)]   // solapan
    [InlineData("14:00", "15:00", "15:00", "16:00", false)]  // back-to-back
    [InlineData("14:00", "15:00", "13:00", "14:00", false)]  // back-to-back inverso
    [InlineData("14:00", "15:00", "13:30", "14:30", true)]   // solapan al inicio
    [InlineData("14:00", "15:00", "13:00", "16:00", true)]   // other contiene
    [InlineData("14:00", "15:00", "14:15", "14:45", true)]   // dentro de this
    public void OverlapsWith_handles_intervals_correctly(
        string s1, string e1, string s2, string e2, bool expectedOverlap)
    {
        var date = new DateTime(2026, 6, 5);
        var start = date.Add(TimeSpan.Parse(s1));
        var appt = Appointment.Create(
            tenantId: Guid.NewGuid(), customerId: Guid.NewGuid(),
            stylistId: Guid.NewGuid(), serviceId: Guid.NewGuid(),
            startAtUtc: start,
            endAtUtc: date.Add(TimeSpan.Parse(e1)),
            priceSnapshot: Money.Create(100000),
            depositPercentage: Percentage.Zero,
            requiresDeposit: false,
            channel: AppointmentChannel.Reception,
            notes: null,
            utcNow: date.Add(TimeSpan.Parse(s1)).AddHours(-1),  // 1h antes
            holdDuration: Hold,
            holdMinBeforeAppointment: HoldMinBefore);

        appt.OverlapsWith(date.Add(TimeSpan.Parse(s2)), date.Add(TimeSpan.Parse(e2)))
            .Should().Be(expectedOverlap);
    }

    // ===== Hold expiration =====

    [Fact]
    public void IsHoldExpired_returns_true_when_past_expiration()
    {
        var appt = NewAppointment(requiresDeposit: true);

        // Hold expira en min(now+3h, startAt-30min). startAt=now+2h → hold=startAt-30min=now+1.5h.
        appt.IsHoldExpired(UtcNow.AddMinutes(30)).Should().BeFalse();
        appt.IsHoldExpired(UtcNow.AddHours(2)).Should().BeTrue();
    }

    [Fact]
    public void IsHoldExpired_is_false_when_no_hold()
    {
        var appt = NewAppointment(requiresDeposit: false);  // Sin hold

        appt.IsHoldExpired(UtcNow.AddYears(10)).Should().BeFalse();
    }
}
