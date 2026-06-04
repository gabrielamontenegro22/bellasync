using BellaSync.Application.Common;
using BellaSync.Application.Common.Handlers;
using BellaSync.Application.Common.Interfaces;
using BellaSync.Application.Common.Results;
using BellaSync.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace BellaSync.Application.Features.Stylists.TimeOff.GetAffectedAppointments;

public sealed class GetAffectedAppointmentsHandler
    : IQueryHandler<GetAffectedAppointmentsQuery, IReadOnlyList<AffectedAppointmentRow>>
{
    private readonly IApplicationDbContext _db;

    public GetAffectedAppointmentsHandler(IApplicationDbContext db) => _db = db;

    public async Task<Result<IReadOnlyList<AffectedAppointmentRow>>> HandleAsync(
        GetAffectedAppointmentsQuery query, CancellationToken ct)
    {
        // Rango [FromDate 00:00 CO, ToDate+1día 00:00 CO) en UTC.
        var (startUtc, _) = ColombiaTime.DayRangeUtc(query.FromDate);
        var (_, endUtc) = ColombiaTime.DayRangeUtc(query.ToDate);

        var affected = await _db.Appointments
            .AsNoTracking()
            .Include(a => a.Customer)
            .Include(a => a.Service)
            .Where(a => a.StylistId == query.StylistId
                     && a.StartAt >= startUtc
                     && a.StartAt < endUtc
                     && a.Status != AppointmentStatus.Cancelled
                     && a.Status != AppointmentStatus.NoShow
                     && a.Status != AppointmentStatus.Completed)
            .OrderBy(a => a.StartAt)
            .Select(a => new AffectedAppointmentRow
            {
                AppointmentId = a.Id,
                CustomerName = a.Customer!.FullName,
                CustomerPhone = a.Customer.Phone,
                ServiceName = a.Service!.Name,
                StartAt = a.StartAt,
                EndAt = a.EndAt,
                Status = a.Status.ToString(),
            })
            .ToListAsync(ct);

        return Result<IReadOnlyList<AffectedAppointmentRow>>.Success(affected);
    }
}
