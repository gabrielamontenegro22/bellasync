using BellaSync.Application.Auth;
using BellaSync.Application.Common.Errors;
using BellaSync.Application.Common.Handlers;
using BellaSync.Application.Common.Interfaces;
using BellaSync.Application.Common.Results;
using BellaSync.Application.Features.Appointments.Dtos;
using BellaSync.Application.Features.Appointments.Shared;
using BellaSync.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BellaSync.Application.Features.Appointments.CreatePublicAppointment;

/// <summary>
/// Endpoint anónimo del portal público. El TenantSlug viene en la URL;
/// el handler resuelve manualmente el tenant (NO usa ICurrentTenantService
/// porque no hay JWT) y usa IgnoreQueryFilters para escapar el filtro
/// global multi-tenant.
///
/// Si el teléfono del cliente es nuevo, crea Customer automáticamente.
/// </summary>
public sealed class CreatePublicAppointmentHandler
    : ICommandHandler<CreatePublicAppointmentCommand, PublicBookingResponse>
{
    private readonly IApplicationDbContext _db;
    private readonly IClock _clock;
    private readonly AppointmentValidator _validator;
    private readonly AppointmentSettings _settings;
    private readonly ILogger<CreatePublicAppointmentHandler> _logger;

    public CreatePublicAppointmentHandler(
        IApplicationDbContext db,
        IClock clock,
        AppointmentValidator validator,
        IOptions<AppointmentSettings> settings,
        ILogger<CreatePublicAppointmentHandler> logger)
    {
        _db = db;
        _clock = clock;
        _validator = validator;
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task<Result<PublicBookingResponse>> HandleAsync(
        CreatePublicAppointmentCommand command, CancellationToken ct)
    {
        // 1. Resolver tenant por slug (anónimo: necesita IgnoreQueryFilters).
        var tenant = await _db.Tenants
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.Slug == command.TenantSlug && t.IsActive, ct);
        if (tenant is null)
            return ApplicationError.NotFound("tenant.not_found", "El salón no existe.");

        // 2. Resolver/crear Customer por phone (también con IgnoreQueryFilters
        //    porque el filtro global multi-tenant no aplica acá).
        var normalizedPhone = command.ClientPhone.Trim();
        var customer = await _db.Customers
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(c => c.TenantId == tenant.Id && c.Phone == normalizedPhone, ct);

        if (customer is null)
        {
            customer = Customer.Create(
                tenantId: tenant.Id,
                fullName: command.ClientName,
                phone: normalizedPhone,
                email: command.ClientEmail,
                acceptsMarketing: false);  // opt-in explícito: default false
            _db.Customers.Add(customer);
        }
        else if (!customer.IsActive)
        {
            // Reactivamos el cliente archivado que vuelve a agendar.
            customer.Reactivate();
        }

        // 3. Validar slot — necesita IgnoreQueryFilters indirectamente,
        //    pero AppointmentValidator queries usan filtros del DbContext.
        //    Como no hay tenant en el JWT, el filtro filtra Guid.Empty.
        //    SOLUCIÓN: el público realmente debe verificar slots EN ESTE tenant.
        //    Usamos un wrapper local que ejecuta con el tenantId del slug.
        var refsResult = await ResolveAndValidateForPublicAsync(
            tenant.Id, command.StylistId, command.ServiceId, command.StartAtUtc, ct);

        if (refsResult.IsFailure) return refsResult.Error!;

        var refs = refsResult.Value!;
        var endAtUtc = command.StartAtUtc.AddMinutes(refs.Service.DurationMinutes);

        var appointment = Appointment.Create(
            tenantId: tenant.Id,
            customerId: customer.Id,
            stylistId: refs.Stylist.Id,
            serviceId: refs.Service.Id,
            startAtUtc: command.StartAtUtc,
            endAtUtc: endAtUtc,
            priceSnapshot: refs.Service.Price,
            depositPercentage: refs.Service.DepositPercentage,
            requiresDeposit: refs.Service.RequiresDeposit,
            channel: AppointmentChannel.PublicPortal,
            notes: null,
            utcNow: _clock.UtcNow,
            holdDuration: TimeSpan.FromHours(_settings.HoldDurationHours),
            holdMinBeforeAppointment: TimeSpan.FromMinutes(_settings.HoldMinBeforeAppointmentMinutes));

        _db.Appointments.Add(appointment);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Cita pública {AppointmentId} creada en tenant {TenantSlug} (status={Status})",
            appointment.Id, tenant.Slug, appointment.Status);

        return Result<PublicBookingResponse>.Success(new PublicBookingResponse
        {
            AppointmentId = appointment.Id,
            StartAt = appointment.StartAt,
            ServiceName = refs.Service.Name,
            StylistName = refs.Stylist.FullName,
            PriceSnapshot = appointment.PriceSnapshot.Amount,
            Status = appointment.Status.ToString(),
            RequiresDeposit = refs.Service.RequiresDeposit,
            DepositAmount = appointment.DepositAmount.Amount,
            HoldExpiresAt = appointment.HoldExpiresAt,
        });
    }

    /// <summary>
    /// Variante del validator que pasa por IgnoreQueryFilters (necesario
    /// porque el endpoint es anónimo y el filtro global rechaza todo).
    /// </summary>
    private async Task<Result<AppointmentValidator.ResolvedRefs>> ResolveAndValidateForPublicAsync(
        Guid tenantId, Guid stylistId, Guid serviceId, DateTime startAtUtc, CancellationToken ct)
    {
        var utcNow = _clock.UtcNow;

        if (startAtUtc < utcNow.AddMinutes(_settings.MinAdvanceMinutes))
            return ApplicationError.Validation(
                "appointment.too_soon",
                $"La cita debe agendarse con al menos {_settings.MinAdvanceMinutes} minutos de anticipación.");

        var service = await _db.Services
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(s => s.TenantId == tenantId && s.Id == serviceId, ct);
        if (service is null)
            return ApplicationError.NotFound("service.not_found", "El servicio no existe.");
        if (!service.IsActive)
            return ApplicationError.Validation("appointment.service_inactive",
                "El servicio no está disponible.");

        var stylist = await _db.Stylists
            .IgnoreQueryFilters()
            .Include(s => s.StylistServices)
            .FirstOrDefaultAsync(s => s.TenantId == tenantId && s.Id == stylistId, ct);
        if (stylist is null)
            return ApplicationError.NotFound("stylist.not_found", "El estilista no existe.");
        if (stylist.Status == StylistStatus.Inactive)
            return ApplicationError.Validation("appointment.stylist_inactive",
                "El estilista ya no forma parte del equipo.");
        if (stylist.Status == StylistStatus.Vacation)
            return ApplicationError.Validation("appointment.stylist_on_vacation",
                "El estilista está en vacaciones y no toma citas.");
        if (!stylist.StylistServices.Any(ss => ss.ServiceId == serviceId))
            return ApplicationError.Validation("appointment.stylist_cant_do_service",
                $"El estilista {stylist.FullName} no realiza este servicio.");

        var endAtUtc = startAtUtc.AddMinutes(service.DurationMinutes);
        var hasOverlap = await _db.Appointments
            .IgnoreQueryFilters()
            .AnyAsync(a => a.TenantId == tenantId
                        && a.StylistId == stylistId
                        && a.Status != AppointmentStatus.Cancelled
                        && a.Status != AppointmentStatus.NoShow
                        && a.StartAt < endAtUtc
                        && a.EndAt > startAtUtc, ct);
        if (hasOverlap)
            return ApplicationError.Conflict("appointment.slot_overlap",
                $"El estilista {stylist.FullName} ya tiene una cita en ese horario.");

        return Result<AppointmentValidator.ResolvedRefs>.Success(
            new AppointmentValidator.ResolvedRefs(service, stylist));
    }
}
