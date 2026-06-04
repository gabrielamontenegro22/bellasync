using BellaSync.Application.Common.Handlers;

namespace BellaSync.Application.Features.WhatsApp.UpdateTemplate;

/// <summary>
/// Actualiza body + isEnabled de UN kind. La URL del controller lleva
/// el kind, el body queda en el JSON: PUT /whatsapp/templates/{kind}.
/// </summary>
public sealed record UpdateWhatsAppTemplateCommand(
    string Kind,
    string Body,
    bool IsEnabled) : ICommand;
