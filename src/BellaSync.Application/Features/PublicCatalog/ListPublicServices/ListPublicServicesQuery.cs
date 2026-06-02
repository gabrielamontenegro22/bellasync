using BellaSync.Application.Common.Handlers;
using BellaSync.Application.Features.PublicCatalog.Dtos;

namespace BellaSync.Application.Features.PublicCatalog.ListPublicServices;

/// <summary>Lista servicios ACTIVOS del salón identificado por slug. Anónimo.</summary>
public sealed record ListPublicServicesQuery(string TenantSlug)
    : IQuery<IReadOnlyList<PublicServiceItem>>;
