using BellaSync.Application.Common.Handlers;
using BellaSync.Application.Features.Stylists.Dtos;

namespace BellaSync.Application.Features.Stylists.GetStylist;

public sealed record GetStylistQuery(Guid Id) : IQuery<StylistResponse>;
