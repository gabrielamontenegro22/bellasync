using BellaSync.Application.Common.Errors;
using BellaSync.Application.Common.Handlers;
using BellaSync.Application.Common.Interfaces;
using BellaSync.Application.Common.Results;
using BellaSync.Application.Features.Customers.Dtos;
using BellaSync.Application.Features.Customers.Shared;
using BellaSync.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace BellaSync.Application.Features.Customers.GetCustomer;

public sealed class GetCustomerHandler : IQueryHandler<GetCustomerQuery, CustomerResponse>
{
    private readonly IApplicationDbContext _db;
    private readonly IClock _clock;

    public GetCustomerHandler(IApplicationDbContext db, IClock clock)
    {
        _db = db;
        _clock = clock;
    }

    public async Task<Result<CustomerResponse>> HandleAsync(GetCustomerQuery query, CancellationToken ct)
    {
        var customer = await _db.Customers.AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == query.Id, ct);

        if (customer is null)
        {
            return ApplicationError.NotFound(
                "customer.not_found",
                $"No existe un cliente con id {query.Id}.");
        }

        var utcNow = _clock.UtcNow;

        // Misma estrategia que ListCustomersHandler: 4 subqueries sobre Appointments
        // (count, last, next, preferred stylist). En memoria son 4 round-trips, pero
        // como es un GET por id de un solo cliente vale la simplicidad sobre un único
        // query con JOIN.
        var visits = await _db.Appointments
            .CountAsync(a => a.CustomerId == customer.Id && a.Status == AppointmentStatus.Completed, ct);

        var lastVisitAt = await _db.Appointments
            .Where(a => a.CustomerId == customer.Id && a.Status == AppointmentStatus.Completed)
            .OrderByDescending(a => a.StartAt)
            .Select(a => (DateTime?)a.StartAt)
            .FirstOrDefaultAsync(ct);

        var nextVisitAt = await _db.Appointments
            .Where(a => a.CustomerId == customer.Id
                && (a.Status == AppointmentStatus.Pending || a.Status == AppointmentStatus.Confirmed)
                && a.StartAt > utcNow)
            .OrderBy(a => a.StartAt)
            .Select(a => (DateTime?)a.StartAt)
            .FirstOrDefaultAsync(ct);

        var preferredStylistName = await _db.Appointments
            .Where(a => a.CustomerId == customer.Id && a.Status == AppointmentStatus.Completed)
            .GroupBy(a => a.Stylist!.FullName)
            .OrderByDescending(g => g.Count())
            .Select(g => g.Key)
            .FirstOrDefaultAsync(ct);

        return Result<CustomerResponse>.Success(
            CustomerMapper.ToResponseWithStats(
                customer, visits, lastVisitAt, nextVisitAt, preferredStylistName, utcNow));
    }
}
