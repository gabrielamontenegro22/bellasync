using BellaSync.Application.Common.Handlers;
using BellaSync.Application.Features.Services.Dtos;

namespace BellaSync.Application.Features.Services.ListServices;

public sealed record ListServicesQuery(bool IncludeInactive) : IQuery<IReadOnlyList<ServiceResponse>>;
