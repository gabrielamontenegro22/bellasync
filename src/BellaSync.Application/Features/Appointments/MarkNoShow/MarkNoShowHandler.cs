using BellaSync.Application.Common.Errors;
using BellaSync.Application.Common.Handlers;
using BellaSync.Application.Common.Interfaces;
using BellaSync.Application.Common.Results;
using BellaSync.Application.Features.Appointments.Dtos;
using BellaSync.Application.Features.Appointments.Shared;
using BellaSync.Domain.Common;
using Microsoft.EntityFrameworkCore;

namespace BellaSync.Application.Features.Appointments.MarkNoShow;

public sealed class MarkNoShowHandler : ICommandHandler<MarkNoShowCommand, AppointmentResponse>
{
    private readonly IApplicationDbContext _db;

    public MarkNoShowHandler(IApplicationDbContext db) => _db = db;

    public async Task<Result<AppointmentResponse>> HandleAsync(
        MarkNoShowCommand command, CancellationToken ct)
    {
        var appointment = await _db.Appointments
            .Include(a => a.Customer).Include(a => a.Stylist).Include(a => a.Service)
            .FirstOrDefaultAsync(a => a.Id == command.Id, ct);

        if (appointment is null)
            return ApplicationError.NotFound("appointment.not_found",
                $"No existe una cita con id {command.Id}.");

        try { appointment.MarkNoShow(); }
        catch (DomainException ex)
        {
            return ApplicationError.Validation("appointment.invalid_transition", ex.Message);
        }

        await _db.SaveChangesAsync(ct);
        return Result<AppointmentResponse>.Success(
            await AppointmentMapper.ToResponseAsync(appointment, _db, ct));
    }
}
