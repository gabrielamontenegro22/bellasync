using BellaSync.Application.Common.Handlers;
using BellaSync.Application.Features.Customers.Dtos;

namespace BellaSync.Application.Features.Customers.CreateCustomer;

public sealed record CreateCustomerCommand(
    string FullName,
    string Phone,
    string? Email,
    DateOnly? Birthday,
    string? DocumentNumber,
    string? Address,
    string? Notes,
    bool AcceptsMarketing) : ICommand<CustomerResponse>;
