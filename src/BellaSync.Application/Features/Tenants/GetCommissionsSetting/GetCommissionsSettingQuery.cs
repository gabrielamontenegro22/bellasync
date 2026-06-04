using BellaSync.Application.Common.Handlers;
using BellaSync.Application.Features.Tenants.Dtos;

namespace BellaSync.Application.Features.Tenants.GetCommissionsSetting;

public sealed record GetCommissionsSettingQuery() : IQuery<CommissionsSettingResponse>;
