using BellaSync.Application.Common.Handlers;

namespace BellaSync.Application.Features.Customers.DeleteCustomer;

public sealed record DeleteCustomerCommand(Guid Id) : ICommand;
