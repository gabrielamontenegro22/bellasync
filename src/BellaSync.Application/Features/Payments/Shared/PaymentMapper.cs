using BellaSync.Application.Features.Payments.Dtos;
using BellaSync.Domain.Entities;

namespace BellaSync.Application.Features.Payments.Shared;

internal static class PaymentMapper
{
    /// <summary>
    /// Mapeo "rico" con info del appointment cargado vía Include.
    /// Requiere que el caller incluya Appointment.Service y .Stylist.
    /// </summary>
    public static PaymentResponse ToResponse(Payment p) => new()
    {
        Id = p.Id,
        AppointmentId = p.AppointmentId,
        Method = p.Method.ToString(),
        Amount = p.Amount.Amount,
        Tip = p.Tip.Amount,
        Total = p.Amount.Amount + p.Tip.Amount,
        Reference = p.Reference,
        RegisteredByUserId = p.RegisteredByUserId,
        RegisteredAt = p.RegisteredAt,
        ServiceName = p.Appointment?.Service?.Name ?? "—",
        StylistName = p.Appointment?.Stylist?.FullName ?? "—",
        AppointmentStartAt = p.Appointment?.StartAt ?? DateTime.MinValue,
    };
}
