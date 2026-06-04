using BellaSync.Application.Common.Handlers;
using BellaSync.Application.Common.Interfaces;
using BellaSync.Application.Common.Results;
using BellaSync.Application.Features.WhatsApp.Dtos;
using BellaSync.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace BellaSync.Application.Features.WhatsApp.GetMessages;

/// <summary>
/// Devuelve mensajes ordenados por QueuedAt desc (los más recientes primero).
/// Filtro opcional por status: "Queued" | "Sent" | "Failed" | "Cancelled".
/// Cap a Take (default 50, max 200) para no traer histórico gigante.
/// </summary>
public sealed class GetWhatsAppMessagesHandler
    : IQueryHandler<GetWhatsAppMessagesQuery, IReadOnlyList<WhatsAppMessageDto>>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentTenantService _currentTenant;

    public GetWhatsAppMessagesHandler(
        IApplicationDbContext db,
        ICurrentTenantService currentTenant)
    {
        _db = db;
        _currentTenant = currentTenant;
    }

    public async Task<Result<IReadOnlyList<WhatsAppMessageDto>>> HandleAsync(
        GetWhatsAppMessagesQuery query, CancellationToken ct)
    {
        var tenantId = _currentTenant.TenantId;
        var take = Math.Clamp(query.Take, 1, 200);

        IQueryable<WhatsAppMessage> q = _db.WhatsAppMessages
            .Where(m => m.TenantId == tenantId);

        if (!string.IsNullOrWhiteSpace(query.Status)
            && Enum.TryParse<WhatsAppMessageStatus>(query.Status, ignoreCase: true, out var status))
        {
            q = q.Where(m => m.Status == status);
        }

        var rows = await q
            .OrderByDescending(m => m.QueuedAt)
            .Take(take)
            .Select(m => new WhatsAppMessageDto
            {
                Id = m.Id,
                Kind = m.Kind.ToString(),
                CustomerPhone = m.CustomerPhone,
                RenderedBody = m.RenderedBody,
                AppointmentId = m.AppointmentId,
                Status = m.Status.ToString(),
                QueuedAt = m.QueuedAt,
                SentAt = m.SentAt,
                FailedAt = m.FailedAt,
                FailureReason = m.FailureReason,
            })
            .ToListAsync(ct);

        return Result<IReadOnlyList<WhatsAppMessageDto>>.Success(rows);
    }
}
