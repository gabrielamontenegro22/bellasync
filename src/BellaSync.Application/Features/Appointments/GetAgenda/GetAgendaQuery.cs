using BellaSync.Application.Common.Handlers;
using BellaSync.Application.Features.Appointments.Dtos;

namespace BellaSync.Application.Features.Appointments.GetAgenda;

/// <summary>
/// Agenda del día: lista de citas + métricas. Opcionalmente filtrada por
/// stylist (StylistId = null devuelve todos los estilistas del tenant).
/// </summary>
public sealed record GetAgendaQuery(
    DateOnly Date,
    Guid? StylistId) : IQuery<AgendaResponse>;
