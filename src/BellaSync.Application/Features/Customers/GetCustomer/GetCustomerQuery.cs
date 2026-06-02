using BellaSync.Application.Common.Handlers;
using BellaSync.Application.Features.Customers.Dtos;

namespace BellaSync.Application.Features.Customers.GetCustomer;

public sealed record GetCustomerQuery(Guid Id) : IQuery<CustomerResponse>;
