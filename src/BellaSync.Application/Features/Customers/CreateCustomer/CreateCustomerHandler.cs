using BellaSync.Application.Common.Errors;
using BellaSync.Application.Common.Handlers;
using BellaSync.Application.Common.Interfaces;
using BellaSync.Application.Common.Results;
using BellaSync.Application.Features.Customers.Dtos;
using BellaSync.Application.Features.Customers.Shared;
using BellaSync.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BellaSync.Application.Features.Customers.CreateCustomer;

public sealed class CreateCustomerHandler : ICommandHandler<CreateCustomerCommand, CustomerResponse>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentTenantService _currentTenant;
    private readonly ILogger<CreateCustomerHandler> _logger;

    public CreateCustomerHandler(
        IApplicationDbContext db,
        ICurrentTenantService currentTenant,
        ILogger<CreateCustomerHandler> logger)
    {
        _db = db;
        _currentTenant = currentTenant;
        _logger = logger;
    }

    public async Task<Result<CustomerResponse>> HandleAsync(
        CreateCustomerCommand command, CancellationToken ct)
    {
        var phone = CustomerMapper.NormalizePhone(command.Phone);

        var existing = await _db.Customers
            .Where(c => c.IsActive && c.Phone == phone)
            .Select(c => new { c.Id, c.FullName })
            .FirstOrDefaultAsync(ct);

        if (existing is not null)
        {
            return ApplicationError.Conflict(
                "customer.phone_taken",
                $"Ya existe un cliente activo con el teléfono {phone}: \"{existing.FullName}\" (id: {existing.Id}).");
        }

        var customer = Customer.Create(
            tenantId: _currentTenant.TenantId,
            fullName: command.FullName,
            phone: phone,
            email: command.Email,
            birthday: command.Birthday,
            documentNumber: command.DocumentNumber,
            address: command.Address,
            notes: command.Notes,
            acceptsMarketing: command.AcceptsMarketing);

        _db.Customers.Add(customer);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Cliente {FullName} ({CustomerId}) creado en tenant {TenantId}",
            customer.FullName, customer.Id, customer.TenantId);

        return Result<CustomerResponse>.Success(CustomerMapper.ToResponse(customer));
    }
}
