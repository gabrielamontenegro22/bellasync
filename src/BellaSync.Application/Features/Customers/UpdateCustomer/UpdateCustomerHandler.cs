using BellaSync.Application.Common.Errors;
using BellaSync.Application.Common.Handlers;
using BellaSync.Application.Common.Interfaces;
using BellaSync.Application.Common.Results;
using BellaSync.Application.Features.Customers.Dtos;
using BellaSync.Application.Features.Customers.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BellaSync.Application.Features.Customers.UpdateCustomer;

public sealed class UpdateCustomerHandler : ICommandHandler<UpdateCustomerCommand, CustomerResponse>
{
    private readonly IApplicationDbContext _db;
    private readonly ILogger<UpdateCustomerHandler> _logger;

    public UpdateCustomerHandler(IApplicationDbContext db, ILogger<UpdateCustomerHandler> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<Result<CustomerResponse>> HandleAsync(
        UpdateCustomerCommand command, CancellationToken ct)
    {
        var customer = await _db.Customers.FirstOrDefaultAsync(c => c.Id == command.Id, ct);
        if (customer is null)
        {
            return ApplicationError.NotFound(
                "customer.not_found",
                $"No existe un cliente con id {command.Id}.");
        }

        var newPhone = CustomerMapper.NormalizePhone(command.Phone);
        var phoneChanged = !string.Equals(customer.Phone, newPhone, StringComparison.Ordinal);

        if (command.IsActive && phoneChanged)
        {
            var phoneTaken = await _db.Customers
                .AnyAsync(c => c.Id != command.Id && c.IsActive && c.Phone == newPhone, ct);
            if (phoneTaken)
            {
                return ApplicationError.Conflict(
                    "customer.phone_taken",
                    $"Ya existe otro cliente activo con el teléfono {newPhone}.");
            }
        }

        customer.Rename(command.FullName);
        customer.UpdateContact(newPhone, command.Email, command.Address);
        customer.UpdateProfile(command.DocumentNumber, command.Birthday, command.Notes);

        if (command.AcceptsMarketing) customer.OptInMarketing();
        else customer.OptOutMarketing();

        if (command.IsActive) customer.Reactivate();
        else customer.Archive();

        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Cliente {CustomerId} actualizado en tenant {TenantId}",
            customer.Id, customer.TenantId);

        return Result<CustomerResponse>.Success(CustomerMapper.ToResponse(customer));
    }
}
