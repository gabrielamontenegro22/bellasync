using BellaSync.Application.Common.Errors;
using BellaSync.Application.Common.Handlers;
using BellaSync.Application.Common.Interfaces;
using BellaSync.Application.Common.Results;
using BellaSync.Application.Features.Appointments.Dtos;
using BellaSync.Application.Features.Appointments.Shared;
using BellaSync.Domain.Common;
using BellaSync.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BellaSync.Application.Features.Appointments.ConfirmAppointment;

public sealed class ConfirmAppointmentHandler
    : ICommandHandler<ConfirmAppointmentCommand, AppointmentResponse>
{
    private readonly IApplicationDbContext _db;
    private readonly ILogger<ConfirmAppointmentHandler> _logger;

    public ConfirmAppointmentHandler(IApplicationDbContext db, ILogger<ConfirmAppointmentHandler> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<Result<AppointmentResponse>> HandleAsync(
        ConfirmAppointmentCommand command, CancellationToken ct)
    {
        var appointment = await _db.Appointments
            .Include(a => a.Customer)
            .Include(a => a.Stylist)
            .Include(a => a.Service)
            .FirstOrDefaultAsync(a => a.Id == command.Id, ct);

        if (appointment is null)
            return ApplicationError.NotFound("appointment.not_found",
                $"No existe una cita con id {command.Id}.");

        try
        {
            // Si la cita requería anticipo, también lo marcamos como validado
            // (la recepción está confirmando manualmente, asumimos que ya verificó).
            if (appointment.DepositStatus == AppointmentDepositStatus.AwaitingPayment)
                appointment.ValidateDeposit();

            appointment.Confirm();
        }
        catch (DomainException ex)
        {
            return ApplicationError.Validation("appointment.invalid_transition", ex.Message);
        }

        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Cita {AppointmentId} confirmada en tenant {TenantId}",
            appointment.Id, appointment.TenantId);

        return Result<AppointmentResponse>.Success(
            await AppointmentMapper.ToResponseAsync(appointment, _db, ct));
    }
}
