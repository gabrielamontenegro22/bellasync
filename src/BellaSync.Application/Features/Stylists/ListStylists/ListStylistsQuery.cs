using BellaSync.Application.Common.Handlers;
using BellaSync.Application.Features.Stylists.Dtos;

namespace BellaSync.Application.Features.Stylists.ListStylists;

public sealed record ListStylistsQuery(bool IncludeInactive) : IQuery<IReadOnlyList<StylistResponse>>;
