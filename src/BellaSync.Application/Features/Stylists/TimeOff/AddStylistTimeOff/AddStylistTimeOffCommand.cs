using BellaSync.Application.Common.Handlers;
using BellaSync.Application.Features.Stylists.TimeOff.Dtos;

namespace BellaSync.Application.Features.Stylists.TimeOff.AddStylistTimeOff;

/// <summary>
/// Agrega un período de vacaciones/día libre para un estilista.
/// Devuelve el TimeOff creado para que el frontend lo añada al listado
/// sin re-fetch.
/// </summary>
public sealed record AddStylistTimeOffCommand(
    Guid StylistId,
    DateOnly FromDate,
    DateOnly ToDate,
    string? Reason) : ICommand<StylistTimeOffResponse>;
