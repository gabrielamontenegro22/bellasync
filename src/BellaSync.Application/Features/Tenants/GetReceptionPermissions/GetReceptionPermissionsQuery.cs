using BellaSync.Application.Common.Handlers;
using BellaSync.Application.Features.Tenants.Dtos;

namespace BellaSync.Application.Features.Tenants.GetReceptionPermissions;

/// <summary>
/// Lee los permisos de recepción del tenant actual.
/// Sin parámetros — usa ICurrentTenantService.
/// </summary>
public sealed record GetReceptionPermissionsQuery() : IQuery<ReceptionPermissionsResponse>;
