using BellaSync.Application.Common.Handlers;
using BellaSync.Application.Features.WhatsApp.Dtos;

namespace BellaSync.Application.Features.WhatsApp.GetMessages;

/// <summary>
/// Lista los últimos N mensajes (default 50) opcionalmente filtrados por status.
/// </summary>
public sealed record GetWhatsAppMessagesQuery(
    string? Status = null,
    int Take = 50
) : IQuery<IReadOnlyList<WhatsAppMessageDto>>;
