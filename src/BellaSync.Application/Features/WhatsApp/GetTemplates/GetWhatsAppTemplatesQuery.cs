using BellaSync.Application.Common.Handlers;
using BellaSync.Application.Features.WhatsApp.Dtos;

namespace BellaSync.Application.Features.WhatsApp.GetTemplates;

public sealed record GetWhatsAppTemplatesQuery() : IQuery<IReadOnlyList<WhatsAppTemplateDto>>;
