using BellaSync.Application.Common.Handlers;
using BellaSync.Application.Features.Services.Dtos;

namespace BellaSync.Application.Features.Services.GetService;

public sealed record GetServiceQuery(Guid Id) : IQuery<ServiceResponse>;
