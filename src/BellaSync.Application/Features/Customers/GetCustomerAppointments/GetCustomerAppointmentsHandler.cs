using BellaSync.Application.Common.Errors;
using BellaSync.Application.Common.Handlers;
using BellaSync.Application.Common.Interfaces;
using BellaSync.Application.Common.Results;
using BellaSync.Application.Features.Appointments.Dtos;
using BellaSync.Application.Features.Appointments.Shared;
using Microsoft.EntityFrameworkCore;

namespace BellaSync.Application.Features.Customers.GetCustomerAppointments;

public sealed class GetCustomerAppointmentsHandler
    : IQueryHandler<GetCustomerAppointmentsQuery, IReadOnlyList<AppointmentResponse>>
{
    private readonly IApplicationDbContext _db;

    public GetCustomerAppointmentsHandler(IApplicationDbContext db) => _db = db;

    public async Task<Result<IReadOnlyList<AppointmentResponse>>> HandleAsync(
        GetCustomerAppointmentsQuery query, CancellationToken ct)
    {
        // Verificamos primero que el cliente exista para devolver 404 explícito
        // (en lugar de una lista vacía, que sería ambigua entre "no existe" y
        // "no tiene historial").
        var customerExists = await _db.Customers
            .AnyAsync(c => c.Id == query.CustomerId, ct);

        if (!customerExists)
        {
            return ApplicationError.NotFound(
                "customer.not_found",
                $"No existe un cliente con id {query.CustomerId}.");
        }

        var appointments = await _db.Appointments
            .AsNoTracking()
            .Include(a => a.Customer)
            .Include(a => a.Stylist)
            .Include(a => a.Service)
            .Where(a => a.CustomerId == query.CustomerId)
            .OrderByDescending(a => a.StartAt)
            .ToListAsync(ct);

        IReadOnlyList<AppointmentResponse> items = appointments
            .Select(AppointmentMapper.ToResponse)
            .ToList();

        return Result<IReadOnlyList<AppointmentResponse>>.Success(items);
    }
}
