using BellaSync.Application.Common.Errors;
using BellaSync.Application.Common.Handlers;
using BellaSync.Application.Common.Interfaces;
using BellaSync.Application.Common.Results;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BellaSync.Application.Features.Customers.DeleteCustomer;

public sealed class DeleteCustomerHandler : ICommandHandler<DeleteCustomerCommand>
{
    private readonly IApplicationDbContext _db;
    private readonly ILogger<DeleteCustomerHandler> _logger;

    public DeleteCustomerHandler(IApplicationDbContext db, ILogger<DeleteCustomerHandler> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<Result> HandleAsync(DeleteCustomerCommand command, CancellationToken ct)
    {
        var customer = await _db.Customers.FirstOrDefaultAsync(c => c.Id == command.Id, ct);
        if (customer is null)
        {
            return ApplicationError.NotFound(
                "customer.not_found",
                $"No existe un cliente con id {command.Id}.");
        }

        if (!customer.IsActive) return Result.Success();

        customer.Archive();
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Cliente {CustomerId} archivado en tenant {TenantId}",
            customer.Id, customer.TenantId);

        return Result.Success();
    }
}
