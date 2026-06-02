using BellaSync.Application.Features.Appointments.ReleaseExpiredHolds;
using BellaSync.Application.Tests.Helpers;
using BellaSync.Domain.Entities;
using BellaSync.Domain.ValueObjects;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace BellaSync.Application.Tests.Features.Appointments;

public class ReleaseExpiredHoldsHandlerTests
{
    private static (AppointmentTestContext, ReleaseExpiredHoldsHandler) Setup()
    {
        var ctx = new AppointmentTestContext();
        var handler = new ReleaseExpiredHoldsHandler(
            ctx.Base.Db, ctx.Base.Clock, ctx.Base.Logger<ReleaseExpiredHoldsHandler>());
        return (ctx, handler);
    }

    private static Appointment SeedAppointment(
        AppointmentTestContext ctx,
        bool requiresDeposit,
        DateTime? startAt = null)
    {
        var seed = ctx.SeedFullSetup(serviceRequiresDeposit: requiresDeposit);
        var start = startAt ?? ctx.Base.Clock.UtcNow.AddHours(5);

        var appt = Appointment.Create(
            tenantId: ctx.TenantId,
            customerId: seed.Customer.Id,
            stylistId: seed.Stylist.Id,
            serviceId: seed.Service.Id,
            startAtUtc: start,
            endAtUtc: start.AddMinutes(60),
            priceSnapshot: seed.Service.Price,
            depositPercentage: seed.Service.DepositPercentage,
            requiresDeposit: requiresDeposit,
            channel: AppointmentChannel.PublicPortal,
            notes: null,
            utcNow: ctx.Base.Clock.UtcNow,
            holdDuration: TimeSpan.FromHours(ctx.AppointmentSettings.HoldDurationHours),
            holdMinBeforeAppointment: TimeSpan.FromMinutes(ctx.AppointmentSettings.HoldMinBeforeAppointmentMinutes));

        ctx.Base.Db.Appointments.Add(appt);
        ctx.Base.Db.SaveChanges();
        return appt;
    }

    [Fact]
    public async Task Release_with_no_expired_holds_returns_zero()
    {
        var (ctx, handler) = Setup();
        using var _ = ctx;

        // Cita en 5h, hold válido todavía
        SeedAppointment(ctx, requiresDeposit: true);

        var result = await handler.HandleAsync(new ReleaseExpiredHoldsCommand(), default);

        result.IsSuccess.Should().BeTrue();
        result.Value!.CancelledCount.Should().Be(0);
    }

    [Fact]
    public async Task Release_cancels_appointments_whose_hold_expired()
    {
        var (ctx, handler) = Setup();
        using var _ = ctx;

        // Cita Pending con hold a punto de expirar
        var appt = SeedAppointment(ctx, requiresDeposit: true);

        // Avanzar el reloj 5h: hold ya venció (era ~now+2.5h en este caso porque
        // startAt=now+5h y hold=min(now+3h, startAt-30min)=now+3h).
        ctx.Base.Clock.Advance(TimeSpan.FromHours(5));

        var result = await handler.HandleAsync(new ReleaseExpiredHoldsCommand(), default);

        result.IsSuccess.Should().BeTrue();
        result.Value!.CancelledCount.Should().Be(1);

        var dbAppt = await ctx.Base.Db.Appointments
            .IgnoreQueryFilters()
            .FirstAsync(a => a.Id == appt.Id);
        dbAppt.Status.Should().Be(AppointmentStatus.Cancelled);
        dbAppt.CancellationReason.Should().Contain("Hold expirado");
    }

    [Fact]
    public async Task Release_does_not_touch_confirmed_appointments()
    {
        var (ctx, handler) = Setup();
        using var _ = ctx;

        // Cita SIN deposit → Confirmed sin hold
        var appt = SeedAppointment(ctx, requiresDeposit: false);

        // Avanzar mucho tiempo
        ctx.Base.Clock.Advance(TimeSpan.FromDays(7));

        var result = await handler.HandleAsync(new ReleaseExpiredHoldsCommand(), default);

        result.IsSuccess.Should().BeTrue();
        result.Value!.CancelledCount.Should().Be(0);

        var dbAppt = await ctx.Base.Db.Appointments
            .IgnoreQueryFilters()
            .FirstAsync(a => a.Id == appt.Id);
        dbAppt.Status.Should().Be(AppointmentStatus.Confirmed);
    }

    [Fact]
    public async Task Release_is_idempotent()
    {
        var (ctx, handler) = Setup();
        using var _ = ctx;
        SeedAppointment(ctx, requiresDeposit: true);
        ctx.Base.Clock.Advance(TimeSpan.FromHours(5));

        // Primera corrida: cancela
        var r1 = await handler.HandleAsync(new ReleaseExpiredHoldsCommand(), default);
        r1.Value!.CancelledCount.Should().Be(1);

        // Segunda corrida: no debe cancelar nada (ya están cancelled)
        var r2 = await handler.HandleAsync(new ReleaseExpiredHoldsCommand(), default);
        r2.Value!.CancelledCount.Should().Be(0);
    }
}
