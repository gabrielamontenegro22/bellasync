using BellaSync.Application.Common.Errors;
using BellaSync.Application.Common.Handlers;
using BellaSync.Application.Common.Interfaces;
using BellaSync.Application.Common.Results;
using BellaSync.Application.Features.Payments.Dtos;
using BellaSync.Application.Features.Payments.Shared;
using Microsoft.EntityFrameworkCore;

namespace BellaSync.Application.Features.Payments.GetCustomerPayments;

public sealed class GetCustomerPaymentsHandler
    : IQueryHandler<GetCustomerPaymentsQuery, IReadOnlyList<PaymentResponse>>
{
    private readonly IApplicationDbContext _db;

    public GetCustomerPaymentsHandler(IApplicationDbContext db) => _db = db;

    public async Task<Result<IReadOnlyList<PaymentResponse>>> HandleAsync(
        GetCustomerPaymentsQuery query, CancellationToken ct)
    {
        // 404 explícito si el cliente no existe — sino "lista vacía" sería
        // ambiguo entre "no existe" y "no tiene pagos".
        var customerExists = await _db.Customers.AnyAsync(c => c.Id == query.CustomerId, ct);
        if (!customerExists)
            return ApplicationError.NotFound(
                "customer.not_found",
                $"No existe un cliente con id {query.CustomerId}.");

        // Pagos cuyas citas pertenezcan al cliente. Includes para que el
        // mapper tenga acceso a Service.Name y Stylist.FullName sin hacer
        // 2N queries.
        var payments = await _db.Payments
            .AsNoTracking()
            .Include(p => p.Appointment)!.ThenInclude(a => a!.Service)
            .Include(p => p.Appointment)!.ThenInclude(a => a!.Stylist)
            .Include(p => p.Appointment)!.ThenInclude(a => a!.Customer)
            .Include(p => p.RegisteredByUser)
            .Where(p => p.Appointment!.CustomerId == query.CustomerId)
            .OrderByDescending(p => p.RegisteredAt)
            .ToListAsync(ct);

        IReadOnlyList<PaymentResponse> items = payments
            .Select(PaymentMapper.ToResponse)
            .ToList();

        return Result<IReadOnlyList<PaymentResponse>>.Success(items);
    }
}
