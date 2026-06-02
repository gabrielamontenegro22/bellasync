using BellaSync.Application.Features.Vouchers.Dtos;
using BellaSync.Domain.Entities;

namespace BellaSync.Application.Features.Vouchers.Shared;

internal static class VoucherMapper
{
    /// <summary>
    /// Mapea PaymentVoucher → VoucherResponse. Requiere que Appointment +
    /// sus navigations (Customer, Service, Stylist) estén cargadas.
    /// Urgency calculada con utcNow vs appointment.StartAt.
    /// </summary>
    public static VoucherResponse ToResponse(PaymentVoucher v, DateTime utcNow)
    {
        var appt = v.Appointment;
        var hoursUntilAppt = appt is null
            ? double.MaxValue
            : (appt.StartAt - utcNow).TotalHours;

        var urgency = hoursUntilAppt switch
        {
            <= 6 => "urgent",
            <= 36 => "tomorrow",
            _ => "week",
        };

        return new VoucherResponse
        {
            Id = v.Id,
            AppointmentId = v.AppointmentId,
            CustomerName = appt?.Customer?.FullName ?? string.Empty,
            CustomerPhone = appt?.Customer?.Phone ?? string.Empty,
            ServiceName = appt?.Service?.Name ?? string.Empty,
            StylistName = appt?.Stylist?.FullName ?? string.Empty,
            AppointmentStartAt = appt?.StartAt ?? DateTime.MinValue,
            AppointmentDepositAmount = appt?.DepositAmount.Amount ?? 0m,
            ReportedAmount = v.ReportedAmount.Amount,
            Bank = v.Bank,
            ReferenceNumber = v.ReferenceNumber,
            SenderName = v.SenderName,
            SenderPhone = v.SenderPhone,
            ImageUrl = v.ImageUrl,
            ReceivedAt = v.ReceivedAt,
            Status = v.Status.ToString(),
            Urgency = urgency,
            DecidedAt = v.DecidedAt,
            DecisionNotes = v.DecisionNotes,
        };
    }
}
