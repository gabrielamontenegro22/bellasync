using BellaSync.Application.Common.Handlers;
using BellaSync.Application.Common.Pagination;
using BellaSync.Application.Features.Customers.Dtos;

namespace BellaSync.Application.Features.Customers.ListCustomers;

public sealed record ListCustomersQuery(
    string? Search,
    int Page,
    int PageSize,
    bool IncludeInactive) : IQuery<PaginatedResponse<CustomerResponse>>;
