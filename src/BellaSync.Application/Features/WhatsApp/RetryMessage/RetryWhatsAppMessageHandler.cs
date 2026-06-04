using BellaSync.Application.Common.Errors;
using BellaSync.Application.Common.Handlers;
using BellaSync.Application.Common.Interfaces;
using BellaSync.Application.Common.Results;
using BellaSync.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace BellaSync.Application.Features.WhatsApp.RetryMessage;

/// <summary>
/// Reintenta un mensaje Failed: lo vuelve a status Queued y el dispatcher
/// lo levantará en el próximo tick. Solo aplica a Failed; cualquier otro
/// estado es 400.
/// </summary>
public sealed class RetryWhatsAppMessageHandler
    : ICommandHandler<RetryWhatsAppMessageCommand>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentTenantService _currentTenant;
    private readonly IClock _clock;

    public RetryWhatsAppMessageHandler(
        IApplicationDbContext db,
        ICurrentTenantService currentTenant,
        IClock clock)
    {
        _db = db;
        _currentTenant = currentTenant;
        _clock = clock;
    }

    public async Task<Result> HandleAsync(
        RetryWhatsAppMessageCommand cmd, CancellationToken ct)
    {
        var tenantId = _currentTenant.TenantId;

        var msg = await _db.WhatsAppMessages
            .FirstOrDefaultAsync(m => m.TenantId == tenantId && m.Id == cmd.MessageId, ct);

        if (msg is null)
            return ApplicationError.NotFound(
                "whatsapp.message_not_found",
                "El mensaje no existe.");

        if (msg.Status != WhatsAppMessageStatus.Failed)
            return ApplicationError.Validation(
                "whatsapp.message_not_failed",
                $"Solo mensajes en estado Failed se pueden reintentar (actual: {msg.Status}).");

        try
        {
            msg.Retry(_clock.UtcNow);
        }
        catch (BellaSync.Domain.Common.DomainException ex)
        {
            return ApplicationError.Validation("whatsapp.invalid_retry", ex.Message);
        }

        await _db.SaveChangesAsync(ct);
        return Result.Success();
    }
}
