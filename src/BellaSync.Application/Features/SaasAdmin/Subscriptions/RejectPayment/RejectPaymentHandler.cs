using BellaSync.Application.Common.Errors;
using BellaSync.Application.Common.Handlers;
using BellaSync.Application.Common.Interfaces;
using BellaSync.Application.Common.Results;
using BellaSync.Domain.Common;
using BellaSync.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BellaSync.Application.Features.SaasAdmin.Subscriptions.RejectPayment;

/// <summary>
/// Rechaza un pago reportado. La factura vuelve a Pending pero conserva
/// los datos reportados (para que el salón vea qué reportó) más una
/// nota con la razón del rechazo (la verá en su pantalla de
/// suscripción).
/// </summary>
public sealed class RejectPaymentHandler : ICommandHandler<RejectPaymentCommand>
{
    private readonly IApplicationDbContext _db;
    private readonly IClock _clock;
    private readonly ILogger<RejectPaymentHandler> _logger;

    public RejectPaymentHandler(
        IApplicationDbContext db,
        IClock clock,
        ILogger<RejectPaymentHandler> logger)
    {
        _db = db;
        _clock = clock;
        _logger = logger;
    }

    public async Task<Result> HandleAsync(RejectPaymentCommand command, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(command.Reason))
            return ApplicationError.Validation(
                "saas_admin.reason_required",
                "La razón del rechazo es obligatoria.");

        var invoice = await _db.SubscriptionInvoices
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(i => i.Id == command.InvoiceId, ct);

        if (invoice is null)
            return ApplicationError.NotFound(
                "subscription.invoice_not_found", "Factura no encontrada.");

        try
        {
            invoice.Reject(command.Reason, _clock.UtcNow);
        }
        catch (DomainException ex)
        {
            return ApplicationError.Conflict("subscription.reject_failed", ex.Message);
        }

        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "SuperAdmin rechazó factura {InvoiceId} del tenant {TenantId}: {Reason}",
            invoice.Id, invoice.TenantId, command.Reason);

        return Result.Success();
    }
}
