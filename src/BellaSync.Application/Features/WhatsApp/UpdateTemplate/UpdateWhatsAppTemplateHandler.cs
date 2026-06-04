using BellaSync.Application.Common.Errors;
using BellaSync.Application.Common.Handlers;
using BellaSync.Application.Common.Interfaces;
using BellaSync.Application.Common.Results;
using BellaSync.Application.Features.WhatsApp;
using BellaSync.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace BellaSync.Application.Features.WhatsApp.UpdateTemplate;

/// <summary>
/// Upsert por (TenantId, Kind):
///   - Si NO existe row → la crea con los values del command
///   - Si existe → actualiza body + isEnabled
///
/// Esto permite que el frontend no diferencie entre "guardar nuevo" y
/// "editar existente" — el endpoint es idempotente.
///
/// Valida que el Kind sea uno conocido del catálogo (sino el enum.Parse
/// tira y devolveríamos un 500 feo). Mejor 400 con mensaje claro.
/// </summary>
public sealed class UpdateWhatsAppTemplateHandler
    : ICommandHandler<UpdateWhatsAppTemplateCommand>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentTenantService _currentTenant;
    private readonly IClock _clock;

    public UpdateWhatsAppTemplateHandler(
        IApplicationDbContext db,
        ICurrentTenantService currentTenant,
        IClock clock)
    {
        _db = db;
        _currentTenant = currentTenant;
        _clock = clock;
    }

    public async Task<Result> HandleAsync(
        UpdateWhatsAppTemplateCommand cmd, CancellationToken ct)
    {
        if (!Enum.TryParse<WhatsAppTemplateKind>(cmd.Kind, ignoreCase: true, out var kind))
        {
            return ApplicationError.Validation(
                "whatsapp.invalid_kind",
                $"Tipo de plantilla desconocido: '{cmd.Kind}'.");
        }

        // Verificar que el kind está en el catálogo (puede haber un valor
        // del enum sin metadata si se agrega a medias).
        if (!WhatsAppTemplateCatalog.All.Any(c => c.Kind == kind))
        {
            return ApplicationError.Validation(
                "whatsapp.kind_not_in_catalog",
                $"El tipo '{cmd.Kind}' no está habilitado.");
        }

        var tenantId = _currentTenant.TenantId;
        var existing = await _db.WhatsAppTemplates
            .FirstOrDefaultAsync(t => t.TenantId == tenantId && t.Kind == kind, ct);

        var utcNow = _clock.UtcNow;

        if (existing is null)
        {
            try
            {
                var created = WhatsAppTemplate.Create(
                    tenantId, kind, cmd.Body, cmd.IsEnabled, utcNow);
                _db.WhatsAppTemplates.Add(created);
            }
            catch (BellaSync.Domain.Common.DomainException ex)
            {
                return ApplicationError.Validation("whatsapp.invalid_body", ex.Message);
            }
        }
        else
        {
            try
            {
                existing.UpdateBody(cmd.Body, utcNow);
                existing.SetEnabled(cmd.IsEnabled, utcNow);
            }
            catch (BellaSync.Domain.Common.DomainException ex)
            {
                return ApplicationError.Validation("whatsapp.invalid_body", ex.Message);
            }
        }

        await _db.SaveChangesAsync(ct);
        return Result.Success();
    }
}
