using BellaSync.Application.Common.Handlers;
using BellaSync.Application.Features.Customers.Dtos;

namespace BellaSync.Application.Features.Customers.UpdateCustomer;

public sealed record UpdateCustomerCommand(
    Guid Id,
    string FullName,
    string Phone,
    string? Email,
    DateOnly? Birthday,
    string? DocumentNumber,
    string? Address,
    string? Notes,
    bool AcceptsMarketing,
    bool IsActive) : ICommand<CustomerResponse>;
