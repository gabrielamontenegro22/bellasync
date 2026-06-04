using BellaSync.Application.Common.Errors;
using BellaSync.Application.Common.Handlers;
using BellaSync.Application.Common.Interfaces;
using BellaSync.Application.Common.Results;
using BellaSync.Application.Features.Subscription.Dtos;
using BellaSync.Application.Features.Subscription.GetSubscription;
using BellaSync.Application.Features.Subscription.IssueInvoice;
using BellaSync.Application.Features.Subscription.MarkInvoicePaid;

namespace BellaSync.Application.Features.Subscription.PayCurrentPeriod;

/// <summary>
/// Orquesta IssueInvoice (idempotente) + MarkInvoicePaid. El frontend
/// llama a este endpoint en vez de manejar las dos calls por separado.
/// </summary>
public sealed class PayCurrentPeriodHandler
    : ICommandHandler<PayCurrentPeriodCommand, SubscriptionResponse>
{
    private readonly ICommandHandler<IssueInvoiceCommand, Guid> _issue;
    private readonly ICommandHandler<MarkInvoicePaidCommand, SubscriptionResponse> _markPaid;

    public PayCurrentPeriodHandler(
        ICommandHandler<IssueInvoiceCommand, Guid> issue,
        ICommandHandler<MarkInvoicePaidCommand, SubscriptionResponse> markPaid)
    {
        _issue = issue;
        _markPaid = markPaid;
    }

    public async Task<Result<SubscriptionResponse>> HandleAsync(
        PayCurrentPeriodCommand command, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(command.PaymentMethod))
            return ApplicationError.Validation(
                "subscription.method_required",
                "El método de pago es obligatorio.");

        var issueResult = await _issue.HandleAsync(new IssueInvoiceCommand(), ct);
        if (issueResult.IsFailure) return issueResult.Error!;

        var payResult = await _markPaid.HandleAsync(
            new MarkInvoicePaidCommand(issueResult.Value, command.PaymentMethod, command.Reference),
            ct);
        return payResult;
    }
}
