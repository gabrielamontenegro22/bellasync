using BellaSync.Application.Auth;
using BellaSync.Application.Features.Appointments.Shared;
using BellaSync.Domain.Entities;
using BellaSync.Domain.ValueObjects;
using Microsoft.Extensions.Options;
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
    public IOptions<AppointmentSettings> AppointmentOptions { get; }
    public AppointmentSettings AppointmentSettings { get; }

    public AppointmentTestContext()
    {
        Base = new HandlerTestContext();
        Validator = new AppointmentValidator(Base.Db);

        AppointmentSettings = new AppointmentSettings
        {
            HoldDurationHours = 3,
            HoldMinBeforeAppointmentMinutes = 30,
            MinAdvanceMinutes = 30,
        };
        AppointmentOptions = Options.Create(AppointmentSettings);

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
