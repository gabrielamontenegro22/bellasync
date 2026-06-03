using BellaSync.Application.Common.Errors;
using BellaSync.Application.Common.Handlers;
using BellaSync.Application.Common.Interfaces;
using BellaSync.Application.Common.Results;
using BellaSync.Application.Features.Appointments.Dtos;
using BellaSync.Application.Features.Appointments.Shared;
using Microsoft.EntityFrameworkCore;

namespace BellaSync.Application.Features.Appointments.GetAppointment;

public sealed class GetAppointmentHandler : IQueryHandler<GetAppointmentQuery, AppointmentResponse>
{
    private readonly IApplicationDbContext _db;

    public GetAppointmentHandler(IApplicationDbContext db) => _db = db;

    public async Task<Result<AppointmentResponse>> HandleAsync(
        GetAppointmentQuery query, CancellationToken ct)
    {
        var appointment = await _db.Appointments
            .AsNoTracking()
            .Include(a => a.Customer).Include(a => a.Stylist).Include(a => a.Service)
            .FirstOrDefaultAsync(a => a.Id == query.Id, ct);

        if (appointment is null)
            return ApplicationError.NotFound("appointment.not_found",
                $"No existe una cita con id {query.Id}.");

        return Result<AppointmentResponse>.Success(
            await AppointmentMapper.ToResponseAsync(appointment, _db, ct));
    }
}
