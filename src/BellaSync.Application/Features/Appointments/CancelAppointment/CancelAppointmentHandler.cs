using BellaSync.Application.Common.Errors;
using BellaSync.Application.Common.Handlers;
using BellaSync.Application.Common.Interfaces;
using BellaSync.Application.Common.Results;
using BellaSync.Application.Features.Appointments.Dtos;
using BellaSync.Application.Features.Appointments.Shared;
using BellaSync.Application.Features.WhatsApp;
using BellaSync.Domain.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BellaSync.Application.Features.Appointments.CancelAppointment;

public sealed class CancelAppointmentHandler
    : ICommandHandler<CancelAppointmentCommand, AppointmentResponse>
{
    private readonly IApplicationDbContext _db;
    private readonly IClock _clock;
    private readonly WhatsAppEnqueuer _whatsApp;
    private readonly ILogger<CancelAppointmentHandler> _logger;

    public CancelAppointmentHandler(
        IApplicationDbContext db,
        IClock clock,
        WhatsAppEnqueuer whatsApp,
        ILogger<CancelAppointmentHandler> logger)
    {
        _db = db;
        _clock = clock;
        _whatsApp = whatsApp;
        _logger = logger;
    }

    public async Task<Result<AppointmentResponse>> HandleAsync(
        CancelAppointmentCommand command, CancellationToken ct)
    {
        var appointment = await _db.Appointments
            .Include(a => a.Customer)
            .Include(a => a.Stylist)
            .Include(a => a.Service)
            .FirstOrDefaultAsync(a => a.Id == command.Id, ct);

        if (appointment is null)
            return ApplicationError.NotFound("appointment.not_found",
                $"No existe una cita con id {command.Id}.");

        try { appointment.Cancel(_clock.UtcNow, command.Reason); }
        catch (DomainException ex)
        {
            return ApplicationError.Validation("appointment.invalid_transition", ex.Message);
        }

        // Cancelar WhatsApp Queued de esta cita: si la cancelación ocurre
        // pocas horas antes, el Reminder24h/Ready2h que el dispatcher ya
        // encoló no debería salir (sería absurdo mandar "te esperamos" a
        // una cliente que canceló). Idempotente: si no hay Queued, no-op.
        await _whatsApp.CancelQueuedForAppointmentAsync(
            appointment.TenantId, appointment.Id, ct);

        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Cita {AppointmentId} cancelada en tenant {TenantId}",
            appointment.Id, appointment.TenantId);

        return Result<AppointmentResponse>.Success(
            await AppointmentMapper.ToResponseAsync(appointment, _db, ct));
    }
}
