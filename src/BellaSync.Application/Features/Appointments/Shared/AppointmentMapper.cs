using BellaSync.Application.Common.Interfaces;
using BellaSync.Application.Features.Appointments.Dtos;
using BellaSync.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace BellaSync.Application.Features.Appointments.Shared;

internal static class AppointmentMapper
{
    /// <summary>
    /// Mapea Appointment → AppointmentResponse. Requiere que las navigations
    /// Customer/Stylist/Service estén cargadas (Include).
    ///
    /// `validatedDepositAmount` debe ser la suma de PaymentVouchers en
    /// estado Confirmed para esta cita. Usar el overload async si querés
    /// que el mapper lo calcule por vos.
    /// </summary>
    public static AppointmentResponse ToResponse(Appointment a, decimal validatedDepositAmount = 0m) => new()
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
        ValidatedDepositAmount = validatedDepositAmount,
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

    /// <summary>
    /// Variante async que calcula ValidatedDepositAmount sumando los
    /// PaymentVouchers Confirmed de la cita. Útil para handlers que
    /// devuelven una sola cita (Get, Create, Confirm, Cancel, etc.).
    ///
    /// Para listas grandes (GetAgenda) preferir el método batch que
    /// hace UNA sola query con GROUP BY en vez de N queries.
    /// </summary>
    public static async Task<AppointmentResponse> ToResponseAsync(
        Appointment a,
        IApplicationDbContext db,
        CancellationToken ct)
    {
        var validatedAmount = await db.PaymentVouchers
            .Where(v => v.AppointmentId == a.Id && v.Status == PaymentVoucherStatus.Validated)
            .SumAsync(v => (decimal?)v.ReportedAmount.Amount, ct) ?? 0m;

        return ToResponse(a, validatedAmount);
    }

    /// <summary>
    /// Batch para listas de citas: hace UNA query GROUP BY que devuelve
    /// el total de anticipos validados por appointmentId, evitando N+1.
    /// Devuelve un dictionary listo para el callsite.
    /// </summary>
    public static async Task<Dictionary<Guid, decimal>> GetValidatedDepositAmountsAsync(
        IReadOnlyCollection<Guid> appointmentIds,
        IApplicationDbContext db,
        CancellationToken ct)
    {
        if (appointmentIds.Count == 0) return new Dictionary<Guid, decimal>();

        var rows = await db.PaymentVouchers
            .Where(v => appointmentIds.Contains(v.AppointmentId)
                     && v.Status == PaymentVoucherStatus.Validated)
            .GroupBy(v => v.AppointmentId)
            .Select(g => new { AppointmentId = g.Key, Total = g.Sum(v => v.ReportedAmount.Amount) })
            .ToListAsync(ct);

        return rows.ToDictionary(r => r.AppointmentId, r => r.Total);
    }
}
