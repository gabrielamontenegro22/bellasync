using BellaSync.Application.Common.Handlers;
using BellaSync.Application.Features.Stylists.TimeOff.Dtos;

namespace BellaSync.Application.Features.Stylists.TimeOff.ListStylistTimeOffs;

/// <summary>Lista los períodos de TimeOff del estilista (futuros + recientes pasados).</summary>
public sealed record ListStylistTimeOffsQuery(Guid StylistId)
    : IQuery<IReadOnlyList<StylistTimeOffResponse>>;
