using BellaSync.Application.Common.Interfaces;
using BellaSync.Application.Features.Appointments.Shared;
using BellaSync.Domain.Entities;
using BellaSync.Domain.ValueObjects;
using NSubstitute;

namespace BellaSync.Application.Tests.Helpers;

/// <summary>
/// Extiende HandlerTestContext con helpers específicos para tests de Citas:
/// seed de Customer/Stylist/Service + setup de AppointmentValidator y settings.
/// </summary>
public sealed class AppointmentTestContext : IDisposable
{
    public HandlerTestContext Base { get; }
    public AppointmentValidator Validator { get; }

    /// <summary>
    /// Validador del horario del salón. Por default los tests usan un
    /// tenant sin SalonWeeklyHours/SalonClosedDates configurados, lo
    /// que técnicamente lo trataría como "todos los días cerrados".
    /// Para evitar romper tests existentes que crean citas a cualquier
    /// hora, los tests pasan `bypass: true` al construir/reschedulear,
    /// y los handlers que reciben `BypassAdvanceWindow` se lo pasan al
    /// scheduleValidator.
    /// </summary>
    public BellaSync.Application.Features.Appointments.Shared.SalonScheduleValidator ScheduleValidator { get; }

    /// <summary>
    /// Settings de pagos por tenant — mock que devuelve los valores
    /// históricos default (3 / 30 / 30). Si un test necesita cambiar
    /// los valores, puede sobreescribir el mock con .Returns(...).
    /// Antes era IOptions&lt;AppointmentSettings&gt;, migrado a service
    /// tras hacer la política configurable por salón.
    /// </summary>
    public ITenantAppointmentSettings AppointmentSettings { get; }

    /// <summary>
    /// Helper de WhatsApp que los handlers usan para encolar mensajes
    /// (ConfirmCreated, etc.) y cancelar Queued al cancelar/reagendar.
    /// En los tests no chequeamos los mensajes — los tenants seedeados
    /// no tienen templates persistidos, así que el helper se sale por
    /// "template deshabilitado" (default OFF para Confirm/Birthday y los
    /// otros por falta de row). Igualmente se inyecta para que el ctor
    /// de los handlers no rompa.
    /// </summary>
    public BellaSync.Application.Features.WhatsApp.WhatsAppEnqueuer WhatsAppEnqueuer { get; }

    public AppointmentTestContext()
    {
        Base = new HandlerTestContext();
        Validator = new AppointmentValidator(Base.Db);
        ScheduleValidator = new BellaSync.Application.Features.Appointments.Shared.SalonScheduleValidator(Base.Db);
        WhatsAppEnqueuer = new BellaSync.Application.Features.WhatsApp.WhatsAppEnqueuer(
            Base.Db, Base.Clock,
            new BellaSync.Application.Common.Services.WhatsAppTemplateRenderer());

        AppointmentSettings = Substitute.For<ITenantAppointmentSettings>();
        AppointmentSettings.GetHoldDurationHoursAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(3));
        AppointmentSettings.GetHoldMinBeforeAppointmentMinutesAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(30));
        AppointmentSettings.GetMinAdvanceMinutesAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(30));
        AppointmentSettings.GetCancellationWindowHoursAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(2));

        // CurrentTenant devuelve un tenant fijo en estos tests.
        Base.CurrentTenant.HasTenant.Returns(true);
        Base.CurrentTenant.TenantId.Returns(TenantId);
    }

    /// <summary>TenantId fijo usado por los tests de Appointments.</summary>
    public Guid TenantId { get; } = Guid.NewGuid();

    /// <summary>
    /// Seedea un tenant + service + stylist (asignado al service) + customer.
    /// Devuelve los Ids para que el test los use.
    /// </summary>
    public SeededEntities SeedFullSetup(bool serviceRequiresDeposit = false)
    {
        var tenant = Tenant.Create("Test Salon", "test-salon");
        // ID forzado al TenantId esperado por CurrentTenant.
        typeof(BellaSync.Domain.Common.BaseEntity)
            .GetProperty(nameof(BellaSync.Domain.Common.BaseEntity.Id))!
            .SetValue(tenant, TenantId);
        Base.Db.Tenants.Add(tenant);

        var service = Service.Create(
            tenantId: TenantId,
            name: "Corte",
            category: ServiceCategory.Cabello,
            durationMinutes: 60,
            price: Money.Create(100000),
            commission: Percentage.Create(15),
            requiresDeposit: serviceRequiresDeposit,
            depositPercentage: serviceRequiresDeposit ? Percentage.Create(50) : Percentage.Zero);
        Base.Db.Services.Add(service);

        var stylist = Stylist.Create(
            tenantId: TenantId,
            fullName: "Andrea",
            role: "Estilista");
        stylist.AssignService(service.Id, Base.Clock.UtcNow);
        Base.Db.Stylists.Add(stylist);

        var customer = Customer.Create(
            tenantId: TenantId,
            fullName: "Maria",
            phone: "3001112222");
        Base.Db.Customers.Add(customer);

        Base.Db.SaveChanges();

        return new SeededEntities(tenant, service, stylist, customer);
    }

    public sealed record SeededEntities(Tenant Tenant, Service Service, Stylist Stylist, Customer Customer);

    public void Dispose() => Base.Dispose();
}
