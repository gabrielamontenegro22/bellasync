using BellaSync.Application.Common.Errors;
using BellaSync.Application.Features.Appointments.CreateAppointment;
using BellaSync.Application.Tests.Helpers;
using BellaSync.Domain.Entities;
using BellaSync.Domain.ValueObjects;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace BellaSync.Application.Tests.Features.Appointments;

public class CreateAppointmentHandlerTests
{
    private static (AppointmentTestContext, CreateAppointmentHandler) Setup(bool requiresDeposit = false)
    {
        var ctx = new AppointmentTestContext();
        var handler = new CreateAppointmentHandler(
            ctx.Base.Db, ctx.Base.CurrentTenant, ctx.Base.Clock,
            ctx.Validator, ctx.ScheduleValidator, ctx.AppointmentSettings,
            ctx.WhatsAppEnqueuer,
            ctx.Base.Logger<CreateAppointmentHandler>());
        return (ctx, handler);
    }

    [Fact]
    public async Task Create_with_no_deposit_returns_confirmed_appointment()
    {
        var (ctx, handler) = Setup();
        using var _ = ctx;
        var seed = ctx.SeedFullSetup(serviceRequiresDeposit: false);

        var startAt = ctx.Base.Clock.UtcNow.AddHours(2);
        var result = await handler.HandleAsync(
            new CreateAppointmentCommand(
                seed.Customer.Id, seed.Stylist.Id, seed.Service.Id, startAt, null),
            default);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Status.Should().Be("Confirmed");
        result.Value.DepositStatus.Should().Be("NotRequired");
    }

    [Fact]
    public async Task Create_rejects_slot_overlap_with_409_conflict()
    {
        var (ctx, handler) = Setup();
        using var _ = ctx;
        var seed = ctx.SeedFullSetup();

        var startAt = ctx.Base.Clock.UtcNow.AddHours(2);

        // Primera cita OK
        var first = await handler.HandleAsync(
            new CreateAppointmentCommand(
                seed.Customer.Id, seed.Stylist.Id, seed.Service.Id, startAt, null), default);
        first.IsSuccess.Should().BeTrue();

        // Segunda cita en mismo slot → overlap (servicio dura 60min)
        var overlap = await handler.HandleAsync(
            new CreateAppointmentCommand(
                seed.Customer.Id, seed.Stylist.Id, seed.Service.Id, startAt.AddMinutes(30), null),
            default);

        overlap.IsFailure.Should().BeTrue();
        overlap.Error!.Type.Should().Be(ApplicationErrorType.Conflict);
        overlap.Error.Code.Should().Be("appointment.slot_overlap");
    }

    [Fact]
    public async Task Create_allows_back_to_back_appointments()
    {
        var (ctx, handler) = Setup();
        using var _ = ctx;
        var seed = ctx.SeedFullSetup();

        var startAt = ctx.Base.Clock.UtcNow.AddHours(2);

        var first = await handler.HandleAsync(
            new CreateAppointmentCommand(
                seed.Customer.Id, seed.Stylist.Id, seed.Service.Id, startAt, null), default);
        first.IsSuccess.Should().BeTrue();

        // Back-to-back: empieza exactamente cuando termina la anterior
        var backToBack = await handler.HandleAsync(
            new CreateAppointmentCommand(
                seed.Customer.Id, seed.Stylist.Id, seed.Service.Id, startAt.AddMinutes(60), null),
            default);

        backToBack.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task Create_rejects_stylist_that_doesnt_perform_service()
    {
        var (ctx, handler) = Setup();
        using var _ = ctx;
        var seed = ctx.SeedFullSetup();

        // Service nuevo que el stylist NO tiene asignado
        var otherService = Service.Create(
            tenantId: ctx.TenantId,
            name: "Manicure",
            category: ServiceCategory.Unas,
            durationMinutes: 45,
            price: Money.Create(30000),
            commission: Percentage.Create(10),
            requiresDeposit: false);
        ctx.Base.Db.Services.Add(otherService);
        await ctx.Base.Db.SaveChangesAsync();

        var result = await handler.HandleAsync(
            new CreateAppointmentCommand(
                seed.Customer.Id, seed.Stylist.Id, otherService.Id,
                ctx.Base.Clock.UtcNow.AddHours(2), null),
            default);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("appointment.stylist_cant_do_service");
    }

    [Fact]
    public async Task Create_rejects_stylist_on_vacation()
    {
        var (ctx, handler) = Setup();
        using var _ = ctx;
        var seed = ctx.SeedFullSetup();

        seed.Stylist.GoOnVacation();
        await ctx.Base.Db.SaveChangesAsync();

        var result = await handler.HandleAsync(
            new CreateAppointmentCommand(
                seed.Customer.Id, seed.Stylist.Id, seed.Service.Id,
                ctx.Base.Clock.UtcNow.AddHours(2), null),
            default);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("appointment.stylist_on_vacation");
    }

    [Fact]
    public async Task Create_rejects_inactive_service()
    {
        var (ctx, handler) = Setup();
        using var _ = ctx;
        var seed = ctx.SeedFullSetup();

        seed.Service.Archive();
        await ctx.Base.Db.SaveChangesAsync();

        var result = await handler.HandleAsync(
            new CreateAppointmentCommand(
                seed.Customer.Id, seed.Stylist.Id, seed.Service.Id,
                ctx.Base.Clock.UtcNow.AddHours(2), null),
            default);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("appointment.service_inactive");
    }

    [Fact]
    public async Task Create_rejects_too_soon_start()
    {
        var (ctx, handler) = Setup();
        using var _ = ctx;
        var seed = ctx.SeedFullSetup();

        // Solo 10 min de anticipación (mínimo es 30 según settings)
        var result = await handler.HandleAsync(
            new CreateAppointmentCommand(
                seed.Customer.Id, seed.Stylist.Id, seed.Service.Id,
                ctx.Base.Clock.UtcNow.AddMinutes(10), null),
            default);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("appointment.too_soon");
    }

    [Fact]
    public async Task Create_with_deposit_returns_pending_awaiting_payment_with_hold()
    {
        var (ctx, handler) = Setup();
        using var _ = ctx;
        var seed = ctx.SeedFullSetup(serviceRequiresDeposit: true);

        var startAt = ctx.Base.Clock.UtcNow.AddHours(5);
        var result = await handler.HandleAsync(
            new CreateAppointmentCommand(
                seed.Customer.Id, seed.Stylist.Id, seed.Service.Id, startAt, null),
            default);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Status.Should().Be("Pending");
        result.Value.DepositStatus.Should().Be("AwaitingPayment");
        result.Value.DepositAmount.Should().Be(50000m); // 50% de 100000
        result.Value.HoldExpiresAt.Should().NotBeNull();

        // Persistido en BD
        var dbAppt = ctx.Base.Db.Appointments.IgnoreQueryFilters().Single();
        dbAppt.HoldExpiresAt.Should().NotBeNull();
    }
}
