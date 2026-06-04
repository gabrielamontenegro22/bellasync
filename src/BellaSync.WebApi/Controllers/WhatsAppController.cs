using BellaSync.Application.Common.Handlers;
using BellaSync.Application.Features.WhatsApp.Dtos;
using BellaSync.Application.Features.WhatsApp.GetMessages;
using BellaSync.Application.Features.WhatsApp.GetTemplates;
using BellaSync.Application.Features.WhatsApp.RetryMessage;
using BellaSync.Application.Features.WhatsApp.UpdateTemplate;
using BellaSync.WebApi.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BellaSync.WebApi.Controllers;

/// <summary>
/// Endpoints de configuración de WhatsApp:
///   - GET /api/Admin/whatsapp/templates  → lista los 5 kinds del catálogo
///   - PUT /api/Admin/whatsapp/templates/{kind}  → upsert body + enabled
///   - GET /api/Admin/whatsapp/messages?status=Queued&take=50  → log
///   - POST /api/Admin/whatsapp/messages/{id}/retry  → reencola un Failed
///
/// Solo SalonAdmin — la recepción no toca configuración de plantillas
/// (las usa indirectamente al crear citas).
/// </summary>
[ApiController]
[Route("api/Admin/whatsapp")]
[Authorize(Roles = "SalonAdmin")]
public class WhatsAppController : ControllerBase
{
    [HttpGet("templates")]
    [ProducesResponseType(typeof(IReadOnlyList<WhatsAppTemplateDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetTemplates(
        [FromServices] IQueryHandler<GetWhatsAppTemplatesQuery, IReadOnlyList<WhatsAppTemplateDto>> handler,
        CancellationToken ct)
    {
        var result = await handler.HandleAsync(new GetWhatsAppTemplatesQuery(), ct);
        return result.ToActionResult();
    }

    [HttpPut("templates/{kind}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UpdateTemplate(
        string kind,
        [FromBody] UpdateWhatsAppTemplateRequest request,
        [FromServices] ICommandHandler<UpdateWhatsAppTemplateCommand> handler,
        CancellationToken ct)
    {
        var cmd = new UpdateWhatsAppTemplateCommand(kind, request.Body, request.IsEnabled);
        var result = await handler.HandleAsync(cmd, ct);
        return result.ToActionResult();
    }

    [HttpGet("messages")]
    [ProducesResponseType(typeof(IReadOnlyList<WhatsAppMessageDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMessages(
        [FromQuery] string? status,
        [FromQuery] int take,
        [FromServices] IQueryHandler<GetWhatsAppMessagesQuery, IReadOnlyList<WhatsAppMessageDto>> handler,
        CancellationToken ct)
    {
        // take=0 (no enviado) → usa default del handler (50). Aseguramos
        // que no se pase un take ridículo desde la URL.
        var effectiveTake = take <= 0 ? 50 : take;
        var result = await handler.HandleAsync(
            new GetWhatsAppMessagesQuery(status, effectiveTake), ct);
        return result.ToActionResult();
    }

    [HttpPost("messages/{id:guid}/retry")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Retry(
        Guid id,
        [FromServices] ICommandHandler<RetryWhatsAppMessageCommand> handler,
        CancellationToken ct)
    {
        var result = await handler.HandleAsync(new RetryWhatsAppMessageCommand(id), ct);
        return result.ToActionResult();
    }
}

public sealed class UpdateWhatsAppTemplateRequest
{
    public string Body { get; set; } = string.Empty;
    public bool IsEnabled { get; set; }
}
