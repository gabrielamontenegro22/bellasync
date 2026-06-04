using BellaSync.Application.Common.Handlers;
using BellaSync.Application.Features.Tenants.Dtos;

namespace BellaSync.Application.Features.Tenants.GetTenantInfo;

public sealed record GetTenantInfoQuery() : IQuery<TenantInfoResponse>;
