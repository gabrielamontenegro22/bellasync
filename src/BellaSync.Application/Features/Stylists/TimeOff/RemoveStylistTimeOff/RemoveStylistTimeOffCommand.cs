using BellaSync.Application.Common.Handlers;

namespace BellaSync.Application.Features.Stylists.TimeOff.RemoveStylistTimeOff;

/// <summary>
/// Borra un período de TimeOff. Idempotente: si no existe (404 silencioso
/// no, devolvemos NotFound para que el frontend pueda mostrar feedback).
/// </summary>
public sealed record RemoveStylistTimeOffCommand(Guid TimeOffId) : ICommand;
