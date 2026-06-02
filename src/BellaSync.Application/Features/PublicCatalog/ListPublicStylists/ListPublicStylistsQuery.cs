using BellaSync.Application.Common.Handlers;
using BellaSync.Application.Features.PublicCatalog.Dtos;

namespace BellaSync.Application.Features.PublicCatalog.ListPublicStylists;

/// <summary>Lista estilistas Active y Vacation (NO Inactive) del salón. Anónimo.</summary>
public sealed record ListPublicStylistsQuery(string TenantSlug)
    : IQuery<IReadOnlyList<PublicStylistItem>>;
