using BellaSync.Application.Common.Handlers;
using BellaSync.Application.Features.Appointments.Dtos;

namespace BellaSync.Application.Features.Customers.GetCustomerAppointments;

/// <summary>
/// Historial completo de citas de un cliente — pasadas y futuras.
/// Usado por el panel de detalle del CRM (tab Historial).
///
/// Devuelve todas las citas (incluye Cancelled / NoShow) ordenadas
/// desc por StartAt para que la más reciente quede arriba.
/// </summary>
public sealed record GetCustomerAppointmentsQuery(Guid CustomerId)
    : IQuery<IReadOnlyList<AppointmentResponse>>;
