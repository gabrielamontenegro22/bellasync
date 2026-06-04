using BellaSync.Application.Common.Errors;
using BellaSync.Application.Common.Handlers;
using BellaSync.Application.Common.Interfaces;
using BellaSync.Application.Common.Results;
using BellaSync.Application.Features.Appointments.Dtos;
using BellaSync.Application.Features.Appointments.Shared;
using BellaSync.Application.Features.WhatsApp;
using BellaSync.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BellaSync.Application.Features.Appointments.CreateAppointment;

public sealed class CreateAppointmentHandler : ICommandHandler<CreateAppointmentCommand, AppointmentResponse>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentTenantService _currentTenant;
    private readonly IClock _clock;
    private readonly AppointmentValidator _validator;
    private readonly SalonScheduleValidator _scheduleValidator;
    private readonly ITenantAppointmentSettings _settings;
    private readonly WhatsAppEnqueuer _whatsApp;
    private readonly ILogger<CreateAppointmentHandler> _logger;

    public CreateAppointmentHandler(
        IApplicationDbContext db,
        ICurrentTenantService currentTenant,
        IClock clock,
        AppointmentValidator validator,
        SalonScheduleValidator scheduleValidator,
        ITenantAppointmentSettings settings,
        WhatsAppEnqueuer whatsApp,
        ILogger<CreateAppointmentHandler> logger)
    {
        _db = db;
        _currentTenant = currentTenant;
        _clock = clock;
        _validator = validator;
        _scheduleValidator = scheduleValidator;
        _settings = settings;
        _whatsApp = whatsApp;
        _logger = logger;
    }

    public async Task<Result<AppointmentResponse>> HandleAsync(
        CreateAppointmentCommand command, CancellationToken ct)
    {
        // Cliente existe en este tenant (el filtro multi-tenant lo asegura).
        var customer = await _db.Customers.FirstOrDefaultAsync(c => c.Id == command.CustomerId, ct);
        if (customer is null)
            return ApplicationError.NotFound("customer.not_found", "El cliente no existe.");
        if (!customer.IsActive)
            return ApplicationError.Validation("customer.inactive",
                "El cliente está archivado.");

        // Resolver y validar service + stylist + overlap.
        // Si bypass activo (walk-in autorizado por admin), pasamos minAdvance=0
        // para que el validator no rechace por "muy próximo". El factory del
        // dominio sigue rechazando startAt en el pasado, así que el peor caso
        // es agendar a "ahora mismo + 1 segundo".
        var minAdvance = command.BypassAdvanceWindow ? 0 : await _settings.GetMinAdvanceMinutesAsync(ct);
        var holdHours = await _settings.GetHoldDurationHoursAsync(ct);
        var holdMinBefore = await _settings.GetHoldMinBeforeAppointmentMinutesAsync(ct);

        var refsResult = await _validator.ResolveAndValidateAsync(
            stylistId: command.StylistId,
            serviceId: command.ServiceId,
            startAtUtc: command.StartAtUtc,
            utcNow: _clock.UtcNow,
            minAdvanceMinutes: minAdvance,
            excludeAppointmentId: null,
            ct: ct);

        if (refsResult.IsFailure) return refsResult.Error!;

        var refs = refsResult.Value!;
        var endAtUtc = command.StartAtUtc.AddMinutes(refs.Service.DurationMinutes);

        // Validar que la franja cae dentro del horario configurado por
        // el salón (día abierto, dentro del rango, no en lunch, no en
        // cierre puntual, no en festivo). El mismo flag de bypass que
        // permite walk-ins se honra acá — la admin puede meter un
        // walk-in fuera de hora si lo necesita.
        var scheduleResult = await _scheduleValidator.ValidateAsync(
            tenantId: _currentTenant.TenantId,
            startUtc: command.StartAtUtc,
            endUtc: endAtUtc,
            bypass: command.BypassAdvanceWindow,
            ct: ct);
        if (scheduleResult.IsFailure) return scheduleResult.Error!;

        var appointment = Appointment.Create(
            tenantId: _currentTenant.TenantId,
            customerId: customer.Id,
            stylistId: refs.Stylist.Id,
            serviceId: refs.Service.Id,
            startAtUtc: command.StartAtUtc,
            endAtUtc: endAtUtc,
            priceSnapshot: refs.Service.Price,
            depositPercentage: refs.Service.DepositPercentage,
            requiresDeposit: refs.Service.RequiresDeposit,
            channel: AppointmentChannel.Reception,
            notes: command.Notes,
            utcNow: _clock.UtcNow,
            holdDuration: TimeSpan.FromHours(holdHours),
            holdMinBeforeAppointment: TimeSpan.FromMinutes(holdMinBefore));

        _db.Appointments.Add(appointment);

        // ConfirmCreated WhatsApp: encolar para que salga al instante en
        // el próximo tick del dispatcher (~2min). Sin esto, el dispatcher
        // solo arma Reminder24h/Ready2h por ventana de tiempo, y la
        // confirmación de agendamiento se perdería para citas a >25h.
        // Se hace en la misma transacción (SaveChangesAsync abajo persiste
        // appointment + mensaje juntos).
        await _whatsApp.EnqueueForAppointmentAsync(
            tenantId: _currentTenant.TenantId,
            appointment: appointment,
            kind: WhatsAppTemplateKind.ConfirmCreated,
            ct: ct);

        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Cita {AppointmentId} creada en tenant {TenantId} (status={Status})",
            appointment.Id, appointment.TenantId, appointment.Status);

        // Releer con includes para que el mapper tenga acceso a las navigations.
        var created = await _db.Appointments
            .AsNoTracking()
            .Include(a => a.Customer)
            .Include(a => a.Stylist)
            .Include(a => a.Service)
            .FirstAsync(a => a.Id == appointment.Id, ct);

        return Result<AppointmentResponse>.Success(
            await AppointmentMapper.ToResponseAsync(created, _db, ct));
    }
}
