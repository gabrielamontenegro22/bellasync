using BellaSync.Application.Common.Handlers;
using BellaSync.Application.Common.Interfaces;
using BellaSync.Application.Common.Results;
using BellaSync.Application.Features.WhatsApp.Dtos;
using BellaSync.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace BellaSync.Application.Features.WhatsApp.GetTemplates;

/// <summary>
/// Devuelve TODOS los kinds del catálogo, mezclando con lo persistido:
///   - Si existe row en BD para ese kind → usa esos values
///   - Si NO existe → usa el default del catálogo (auto-seed implícito)
///
/// Así el frontend siempre recibe los 5 kinds en el orden definido por
/// el catálogo, sin importar cuántos haya persistido el tenant. La
/// primera vez que la admin guarda algo, los rows reales se crean en
/// UpdateWhatsAppTemplate.
/// </summary>
public sealed class GetWhatsAppTemplatesHandler
    : IQueryHandler<GetWhatsAppTemplatesQuery, IReadOnlyList<WhatsAppTemplateDto>>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentTenantService _currentTenant;

    public GetWhatsAppTemplatesHandler(
        IApplicationDbContext db,
        ICurrentTenantService currentTenant)
    {
        _db = db;
        _currentTenant = currentTenant;
    }

    public async Task<Result<IReadOnlyList<WhatsAppTemplateDto>>> HandleAsync(
        GetWhatsAppTemplatesQuery query, CancellationToken ct)
    {
        var tenantId = _currentTenant.TenantId;

        // Levanta los persistidos en un diccionario por kind para lookup O(1).
        var persisted = await _db.WhatsAppTemplates
            .Where(t => t.TenantId == tenantId)
            .ToDictionaryAsync(t => t.Kind, ct);

        var result = WhatsAppTemplateCatalog.All
            .Select(entry =>
            {
                var hasRow = persisted.TryGetValue(entry.Kind, out var row);
                return new WhatsAppTemplateDto
                {
                    Kind = entry.Kind.ToString(),
                    Title = entry.Title,
                    Description = entry.Description,
                    Body = hasRow ? row!.Body : entry.DefaultBody,
                    IsEnabled = hasRow ? row!.IsEnabled : entry.DefaultEnabled,
                };
            })
            .ToList();

        return Result<IReadOnlyList<WhatsAppTemplateDto>>.Success(result);
    }
}
