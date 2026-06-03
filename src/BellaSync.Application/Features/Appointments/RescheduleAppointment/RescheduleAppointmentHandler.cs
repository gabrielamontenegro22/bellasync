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

namespace BellaSync.Application.Features.Appointments.RescheduleAppointment;

public sealed class RescheduleAppointmentHandler
    : ICommandHandler<RescheduleAppointmentCommand, AppointmentResponse>
{
    private readonly IApplicationDbContext _db;
    private readonly IClock _clock;
    private readonly AppointmentValidator _validator;
    private readonly AppointmentSettings _settings;
    private readonly ILogger<RescheduleAppointmentHandler> _logger;

    public RescheduleAppointmentHandler(
        IApplicationDbContext db,
        IClock clock,
        AppointmentValidator validator,
        IOptions<AppointmentSettings> settings,
        ILogger<RescheduleAppointmentHandler> logger)
    {
        _db = db;
        _clock = clock;
        _validator = validator;
        _settings = settings.Value;
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
        var minAdvance = command.BypassAdvanceWindow ? 0 : _settings.MinAdvanceMinutes;

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

        // Delegamos al dominio: éste valida estado + nueva hora > now y
        // recalcula EndAt + hold.
        try
        {
            appointment.Reschedule(
                newStartAtUtc: command.NewStartAtUtc,
                utcNow: _clock.UtcNow,
                holdDuration: TimeSpan.FromHours(_settings.HoldDurationHours),
                holdMinBeforeAppointment: TimeSpan.FromMinutes(_settings.HoldMinBeforeAppointmentMinutes));
        }
        catch (BellaSync.Domain.Common.DomainException ex)
        {
            return ApplicationError.Validation("appointment.reschedule_invalid", ex.Message);
        }

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
