using BellaSync.Application.Common.Errors;
using BellaSync.Application.Common.Handlers;
using BellaSync.Application.Common.Interfaces;
using BellaSync.Application.Common.Results;
using BellaSync.Application.Features.Subscription.Dtos;
using BellaSync.Application.Features.Subscription.GetSubscription;
using BellaSync.Application.Features.Subscription.IssueInvoice;
using BellaSync.Domain.Common;
using BellaSync.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BellaSync.Application.Features.Subscription.ReportPayment;

/// <summary>
/// Maneja el "Confirmar transferencia" que clickea la admin del salón.
///
/// Pasos:
///   1. Si no hay factura pendiente del tenant, emite una (delega en
///      IssueInvoiceHandler, idempotente).
///   2. Llama a invoice.ReportPayment(method, reference, now) sobre la
///      factura resultante.
///   3. Devuelve el snapshot fresco — el frontend mostrará el banner
///      "Tu pago está en validación".
///
/// La suscripción permanece en Trial/Active/PastDue. La transición a
/// Active solo ocurre cuando el SuperAdmin invoque ValidatePayment.
/// </summary>
public sealed class ReportPaymentHandler
    : ICommandHandler<ReportPaymentCommand, SubscriptionResponse>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentTenantService _currentTenant;
    private readonly IClock _clock;
    private readonly ICommandHandler<IssueInvoiceCommand, Guid> _issue;
    private readonly IQueryHandler<GetSubscriptionQuery, SubscriptionResponse> _getSub;
    private readonly ILogger<ReportPaymentHandler> _logger;

    public ReportPaymentHandler(
        IApplicationDbContext db,
        ICurrentTenantService currentTenant,
        IClock clock,
        ICommandHandler<IssueInvoiceCommand, Guid> issue,
        IQueryHandler<GetSubscriptionQuery, SubscriptionResponse> getSub,
        ILogger<ReportPaymentHandler> logger)
    {
        _db = db;
        _currentTenant = currentTenant;
        _clock = clock;
        _issue = issue;
        _getSub = getSub;
        _logger = logger;
    }

    public async Task<Result<SubscriptionResponse>> HandleAsync(
        ReportPaymentCommand command, CancellationToken ct)
    {
        if (!_currentTenant.HasTenant)
            return ApplicationError.Unauthorized(
                "subscription.no_tenant", "Sesión inválida.");

        if (string.IsNullOrWhiteSpace(command.PaymentMethod))
            return ApplicationError.Validation(
                "subscription.method_required",
                "El método de pago es obligatorio.");

        // Emite (o devuelve la pending existente)
        var issueResult = await _issue.HandleAsync(new IssueInvoiceCommand(), ct);
        if (issueResult.IsFailure) return issueResult.Error!;

        var invoiceId = issueResult.Value;
        var invoice = await _db.SubscriptionInvoices
            .FirstOrDefaultAsync(i => i.Id == invoiceId, ct);

        if (invoice is null)
            return ApplicationError.NotFound(
                "subscription.invoice_not_found",
                "Factura no encontrada.");

        try
        {
            invoice.ReportPayment(command.PaymentMethod, command.Reference, _clock.UtcNow);
        }
        catch (DomainException ex)
        {
            return ApplicationError.Conflict("subscription.report_rejected", ex.Message);
        }

        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Tenant {TenantId} reportó pago de factura {InvoiceId} vía {Method} ref {Ref}",
            _currentTenant.TenantId, invoice.Id, command.PaymentMethod, command.Reference);

        return await _getSub.HandleAsync(new GetSubscriptionQuery(), ct);
    }
}
