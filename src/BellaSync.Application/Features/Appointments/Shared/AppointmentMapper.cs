using BellaSync.Application.Features.Appointments.Dtos;
using BellaSync.Domain.Entities;

namespace BellaSync.Application.Features.Appointments.Shared;

internal static class AppointmentMapper
{
    /// <summary>
    /// Mapea Appointment → AppointmentResponse. Requiere que las navigations
    /// Customer/Stylist/Service estén cargadas (Include).
    /// </summary>
    public static AppointmentResponse ToResponse(Appointment a) => new()
    {
        Id = a.Id,
        CustomerId = a.CustomerId,
        CustomerName = a.Customer?.FullName ?? string.Empty,
        CustomerPhone = a.Customer?.Phone ?? string.Empty,
        StylistId = a.StylistId,
        StylistName = a.Stylist?.FullName ?? string.Empty,
        StylistColor = a.Stylist?.Color,
        ServiceId = a.ServiceId,
        ServiceName = a.Service?.Name ?? string.Empty,
        ServiceCategory = a.Service?.Category.ToString() ?? string.Empty,
        DurationMinutes = a.Service?.DurationMinutes ?? 0,
        ServiceColor = a.Service?.Color,
        StartAt = a.StartAt,
        EndAt = a.EndAt,
        PriceSnapshot = a.PriceSnapshot.Amount,
        DepositPercentage = a.DepositPercentage.Value,
        DepositAmount = a.DepositAmount.Amount,
        Status = a.Status.ToString(),
        DepositStatus = a.DepositStatus.ToString(),
        Channel = a.Channel.ToString(),
        HoldExpiresAt = a.HoldExpiresAt,
        Notes = a.Notes,
        CancelledAt = a.CancelledAt,
        CancellationReason = a.CancellationReason,
        StartedAt = a.StartedAt,
        CompletedAt = a.CompletedAt,
        CreatedAt = a.CreatedAt,
        UpdatedAt = a.UpdatedAt,
    };
}
