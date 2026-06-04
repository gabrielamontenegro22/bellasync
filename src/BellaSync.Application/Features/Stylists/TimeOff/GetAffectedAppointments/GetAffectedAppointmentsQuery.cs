using BellaSync.Application.Common.Handlers;
using BellaSync.Application.Features.Stylists.TimeOff.Dtos;

namespace BellaSync.Application.Features.Stylists.TimeOff.GetAffectedAppointments;

/// <summary>
/// Devuelve citas vigentes (no canceladas) del estilista en el rango
/// dado. Usado para mostrarle a la admin qué citas necesita reagendar
/// al marcar vacaciones.
///
/// Sin StylistTimeOff persistido — toma fechas arbitrarias para que
/// también sirva de "preview" antes de confirmar la creación.
/// </summary>
public sealed record GetAffectedAppointmentsQuery(
    Guid StylistId,
    DateOnly FromDate,
    DateOnly ToDate) : IQuery<IReadOnlyList<AffectedAppointmentRow>>;

public sealed class AffectedAppointmentRow
{
    public Guid AppointmentId { get; init; }
    public string CustomerName { get; init; } = string.Empty;
    public string? CustomerPhone { get; init; }
    public string ServiceName { get; init; } = string.Empty;
    public DateTime StartAt { get; init; }
    public DateTime EndAt { get; init; }
    public string Status { get; init; } = string.Empty;
}
