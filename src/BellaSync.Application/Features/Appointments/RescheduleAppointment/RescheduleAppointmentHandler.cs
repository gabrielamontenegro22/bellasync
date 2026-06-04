using BellaSync.Application.Common.Errors;
using BellaSync.Application.Common.Handlers;
using BellaSync.Application.Common.Interfaces;
using BellaSync.Application.Common.Results;
using BellaSync.Application.Features.Appointments.Dtos;
using BellaSync.Application.Features.Appointments.Shared;
using BellaSync.Application.Features.WhatsApp;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BellaSync.Application.Features.Appointments.RescheduleAppointment;

public sealed class RescheduleAppointmentHandler
    : ICommandHandler<RescheduleAppointmentCommand, AppointmentResponse>
{
    private readonly IApplicationDbContext _db;
    private readonly IClock _clock;
    private readonly AppointmentValidator _validator;
    private readonly SalonScheduleValidator _scheduleValidator;
    private readonly ITenantAppointmentSettings _settings;
    private readonly WhatsAppEnqueuer _whatsApp;
    private readonly ILogger<RescheduleAppointmentHandler> _logger;

    public RescheduleAppointmentHandler(
        IApplicationDbContext db,
        IClock clock,
        AppointmentValidator validator,
        SalonScheduleValidator scheduleValidator,
        ITenantAppointmentSettings settings,
        WhatsAppEnqueuer whatsApp,
        ILogger<RescheduleAppointmentHandler> logger)
    {
        _db = db;
        _clock = clock;
        _validator = validator;
        _scheduleValidator = scheduleValidator;
        _settings = settings;
        _whatsApp = whatsApp;
        _logger = logger;
    }

    public async Task<Result<AppointmentResponse>> HandleAsync(
        RescheduleAppointmentCommand command, CancellationToken ct)
    {
        var appointment = await _db.Appointments
            .FirstOrDefaultAsync(a => a.Id == command.Id, ct);
        if (appointment is null)
            return ApplicationError.NotFound("appointment.not_found", "La cita no existe.");

        // Si el bypass está activo y el caller es admin, pasamos minAdvance=0.
        // El controller silencia bypass si el rol no es SalonAdmin antes de llegar acá.
        var minAdvance = command.BypassAdvanceWindow ? 0 : await _settings.GetMinAdvanceMinutesAsync(ct);
        var holdHours = await _settings.GetHoldDurationHoursAsync(ct);
        var holdMinBefore = await _settings.GetHoldMinBeforeAppointmentMinutesAsync(ct);

        // Validamos overlap excluyendo la propia cita (sino se choca consigo misma).
        // Reusamos el validator del Create — pero acá no resolvemos cliente nuevo;
        // mantenemos service+stylist actuales para chequear que el slot nuevo está libre.
        var refsResult = await _validator.ResolveAndValidateAsync(
            stylistId: appointment.StylistId,
            serviceId: appointment.ServiceId,
            startAtUtc: command.NewStartAtUtc,
            utcNow: _clock.UtcNow,
            minAdvanceMinutes: minAdvance,
            excludeAppointmentId: appointment.Id,
            ct: ct);

        if (refsResult.IsFailure) return refsResult.Error!;

        // Validar que la nueva franja cae dentro del horario configurado
        // por el salón. Misma lógica que en CreateAppointment: bypass
        // honra el flag del comando para que SalonAdmin pueda forzar.
        var newEndUtc = command.NewStartAtUtc.AddMinutes(refsResult.Value!.Service.DurationMinutes);
        var scheduleResult = await _scheduleValidator.ValidateAsync(
            tenantId: appointment.TenantId,
            startUtc: command.NewStartAtUtc,
            endUtc: newEndUtc,
            bypass: command.BypassAdvanceWindow,
            ct: ct);
        if (scheduleResult.IsFailure) return scheduleResult.Error!;

        // Delegamos al dominio: éste valida estado + nueva hora > now y
        // recalcula EndAt + hold.
        try
        {
            appointment.Reschedule(
                newStartAtUtc: command.NewStartAtUtc,
                utcNow: _clock.UtcNow,
                holdDuration: TimeSpan.FromHours(holdHours),
                holdMinBeforeAppointment: TimeSpan.FromMinutes(holdMinBefore));
        }
        catch (BellaSync.Domain.Common.DomainException ex)
        {
            return ApplicationError.Validation("appointment.reschedule_invalid", ex.Message);
        }

        // Cancelar mensajes Queued de la fecha vieja — el Reminder24h /
        // Ready2h que el dispatcher armó tenían la hora anterior. El
        // dispatcher re-encolará los nuevos cuando la fecha nueva entre
        // en ventana. Idempotente: si no había Queued, no-op.
        await _whatsApp.CancelQueuedForAppointmentAsync(
            appointment.TenantId, appointment.Id, ct);

        // Encolar AppointmentRescheduled con la nueva fecha/hora. Es un
        // Kind separado de ConfirmCreated así no choca con la idempotencia
        // (si la cita ya tenía un ConfirmCreated enviado, igualmente este
        // mensaje sale porque es otro Kind).
        await _whatsApp.EnqueueForAppointmentAsync(
            tenantId: appointment.TenantId,
            appointment: appointment,
            kind: BellaSync.Domain.Entities.WhatsAppTemplateKind.AppointmentRescheduled,
            ct: ct);

        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Cita {AppointmentId} reagendada a {NewStart} en tenant {TenantId}",
            appointment.Id, appointment.StartAt, appointment.TenantId);

        // Releer con includes para que el mapper tenga las navigations.
        var updated = await _db.Appointments
            .AsNoTracking()
            .Include(a => a.Customer)
            .Include(a => a.Stylist)
            .Include(a => a.Service)
            .FirstAsync(a => a.Id == appointment.Id, ct);

        return Result<AppointmentResponse>.Success(
            await AppointmentMapper.ToResponseAsync(updated, _db, ct));
    }
}
