using BellaSync.Application.Common.Handlers;
using BellaSync.Application.Common.Interfaces;
using BellaSync.Application.Common.Pagination;
using BellaSync.Application.Common.Results;
using BellaSync.Application.Features.Customers.Dtos;
using BellaSync.Application.Features.Customers.Shared;
using Microsoft.EntityFrameworkCore;

namespace BellaSync.Application.Features.Customers.ListCustomers;

public sealed class ListCustomersHandler
    : IQueryHandler<ListCustomersQuery, PaginatedResponse<CustomerResponse>>
{
    private const int MaxPageSize = 100;
    private const int DefaultPageSize = 20;

    private readonly IApplicationDbContext _db;

    public ListCustomersHandler(IApplicationDbContext db) => _db = db;

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

        var items = await dbQuery
            .OrderBy(c => c.FullName)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(c => CustomerMapper.ToResponse(c))
            .ToListAsync(ct);

        return Result<PaginatedResponse<CustomerResponse>>.Success(
            PaginatedResponse<CustomerResponse>.Create(items, page, pageSize, totalItems));
    }
}
