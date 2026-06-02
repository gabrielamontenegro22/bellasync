using BellaSync.Application.Common.Errors;
using BellaSync.Application.Common.Handlers;
using BellaSync.Application.Common.Interfaces;
using BellaSync.Application.Common.Results;
using BellaSync.Application.Features.Customers.Dtos;
using BellaSync.Application.Features.Customers.Shared;
using Microsoft.EntityFrameworkCore;

namespace BellaSync.Application.Features.Customers.GetCustomer;

public sealed class GetCustomerHandler : IQueryHandler<GetCustomerQuery, CustomerResponse>
{
    private readonly IApplicationDbContext _db;

    public GetCustomerHandler(IApplicationDbContext db) => _db = db;

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

        return Result<CustomerResponse>.Success(CustomerMapper.ToResponse(customer));
    }
}
