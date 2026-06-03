using BellaSync.Application.Common.Handlers;
using BellaSync.Application.Common.Interfaces;
using BellaSync.Application.Common.Pagination;
using BellaSync.Application.Common.Results;
using BellaSync.Application.Features.Customers.Dtos;
using BellaSync.Application.Features.Customers.Shared;
using BellaSync.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace BellaSync.Application.Features.Customers.ListCustomers;

public sealed class ListCustomersHandler
    : IQueryHandler<ListCustomersQuery, PaginatedResponse<CustomerResponse>>
{
    private const int MaxPageSize = 100;
    private const int DefaultPageSize = 20;

    private readonly IApplicationDbContext _db;
    private readonly IClock _clock;

    public ListCustomersHandler(IApplicationDbContext db, IClock clock)
    {
        _db = db;
        _clock = clock;
    }

    public async Task<Result<PaginatedResponse<CustomerResponse>>> HandleAsync(
        ListCustomersQuery query, CancellationToken ct)
    {
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize <= 0 ? DefaultPageSize : query.PageSize, 1, MaxPageSize);

        var dbQuery = _db.Customers.AsNoTracking();

        if (!query.IncludeInactive)
            dbQuery = dbQuery.Where(c => c.IsActive);

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            // Application no conoce el proveedor de BD. Búsqueda case-insensitive
            // con ToLower() + Contains funciona en cualquier provider (PG, SQL Server,
            // SQLite). EF lo traduce a SQL nativo (`WHERE LOWER(full_name) LIKE ...`).
            var term = query.Search.Trim().ToLower();
            dbQuery = dbQuery.Where(c =>
                c.FullName.ToLower().Contains(term) ||
                c.Phone.ToLower().Contains(term));
        }

        var totalItems = await dbQuery.CountAsync(ct);

        var utcNow = _clock.UtcNow;

        // Proyección con subqueries: cuenta de completadas, última visita,
        // próxima visita y estilista preferido. EF traduce cada subquery a
        // un LATERAL/correlated SELECT en PG; con el índice (tenant_id,
        // customer_id, status, start_at) que tiene la tabla de citas son
        // baratas incluso con cientos de clientes.
        var raw = await dbQuery
            .OrderBy(c => c.FullName)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(c => new
            {
                Customer = c,
                Visits = _db.Appointments
                    .Count(a => a.CustomerId == c.Id && a.Status == AppointmentStatus.Completed),
                LastVisitAt = _db.Appointments
                    .Where(a => a.CustomerId == c.Id && a.Status == AppointmentStatus.Completed)
                    .OrderByDescending(a => a.StartAt)
                    .Select(a => (DateTime?)a.StartAt)
                    .FirstOrDefault(),
                NextVisitAt = _db.Appointments
                    .Where(a => a.CustomerId == c.Id
                        && (a.Status == AppointmentStatus.Pending || a.Status == AppointmentStatus.Confirmed)
                        && a.StartAt > utcNow)
                    .OrderBy(a => a.StartAt)
                    .Select(a => (DateTime?)a.StartAt)
                    .FirstOrDefault(),
                PreferredStylistName = _db.Appointments
                    .Where(a => a.CustomerId == c.Id && a.Status == AppointmentStatus.Completed)
                    .GroupBy(a => a.Stylist!.FullName)
                    .OrderByDescending(g => g.Count())
                    .Select(g => g.Key)
                    .FirstOrDefault(),
            })
            .ToListAsync(ct);

        var items = raw
            .Select(r => CustomerMapper.ToResponseWithStats(
                r.Customer, r.Visits, r.LastVisitAt, r.NextVisitAt, r.PreferredStylistName, utcNow))
            .ToList();

        return Result<PaginatedResponse<CustomerResponse>>.Success(
            PaginatedResponse<CustomerResponse>.Create(items, page, pageSize, totalItems));
    }
}
