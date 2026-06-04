using BellaSync.Application.Common.Handlers;

namespace BellaSync.Application.Features.WhatsApp.RetryMessage;

public sealed record RetryWhatsAppMessageCommand(Guid MessageId) : ICommand;
