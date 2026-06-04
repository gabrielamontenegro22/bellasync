using BellaSync.Application.Common;
using BellaSync.Application.Common.Handlers;
using BellaSync.Application.Common.Interfaces;
using BellaSync.Application.Common.Results;
using BellaSync.Application.Features.Appointments.Dtos;
using BellaSync.Application.Features.Appointments.Shared;
using BellaSync.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace BellaSync.Application.Features.Appointments.GetAgenda;

public sealed class GetAgendaHandler : IQueryHandler<GetAgendaQuery, AgendaResponse>
{
    private readonly IApplicationDbContext _db;

    public GetAgendaHandler(IApplicationDbContext db) => _db = db;

    public async Task<Result<AgendaResponse>> HandleAsync(
        GetAgendaQuery query, CancellationToken ct)
    {
        // Rango [00:00, 24:00) en hora COLOMBIA convertido a UTC.
        // Bug histórico C7: usar DateTimeKind.Utc desplazaba citas 19:00-23:59
        // al día siguiente. Ahora compartimos el helper con CashClosing/Reports.
        var (dayStart, dayEnd) = ColombiaTime.DayRangeUtc(query.Date);

        var dbQuery = _db.Appointments
            .AsNoTracking()
            .Include(a => a.Customer).Include(a => a.Stylist).Include(a => a.Service)
            .Where(a => a.StartAt >= dayStart && a.StartAt < dayEnd);

        if (query.StylistId is { } stylistId)
            dbQuery = dbQuery.Where(a => a.StylistId == stylistId);

        var appointments = await dbQuery.OrderBy(a => a.StartAt).ToListAsync(ct);

        var metrics = new AgendaMetrics
        {
            Total = appointments.Count(a => a.Status != AppointmentStatus.Cancelled),
            PendingValidation = appointments.Count(a =>
                a.Status == AppointmentStatus.Pending
                && a.DepositStatus == AppointmentDepositStatus.AwaitingPayment),
            Confirmed = appointments.Count(a =>
                a.Status == AppointmentStatus.Confirmed
                || a.Status == AppointmentStatus.InProgress),
            NoShow = appointments.Count(a => a.Status == AppointmentStatus.NoShow),
        };

        // Batch para evitar N+1: 1 sola query agrupada por cita devuelve
        // el total de anticipos validados de todas las citas del día.
        var validatedAmounts = await AppointmentMapper.GetValidatedDepositAmountsAsync(
            appointments.Select(a => a.Id).ToList(), _db, ct);

        var response = new AgendaResponse
        {
            Date = query.Date,
            Metrics = metrics,
            Appointments = appointments
                .Select(a => AppointmentMapper.ToResponse(
                    a, validatedAmounts.TryGetValue(a.Id, out var v) ? v : 0m))
                .ToList(),
        };

        return Result<AgendaResponse>.Success(response);
    }
}
