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
    public static AppointmentResponse ToResponse(
        Appointment a,
        decimal validatedDepositAmount = 0m,
        decimal directPaymentsTotal = 0m) => new()
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
        DirectPaymentsTotal = directPaymentsTotal,
        Status = a.Status.ToString(),
        DepositStatus = a.DepositStatus.ToString(),
        Channel = a.Channel.ToString(),
        HoldExpiresAt = a.HoldExpiresAt,
        Notes = a.Notes,
        CancelledAt = a.CancelledAt,
        CancellationReason = a.CancellationReason,
        CancelledByUserName = a.CancelledByUser?.FullName,
        StartedAt = a.StartedAt,
        CompletedAt = a.CompletedAt,
        CreatedAt = a.CreatedAt,
        UpdatedAt = a.UpdatedAt,
    };

    /// <summary>
    /// Variante async que calcula ValidatedDepositAmount + DirectPaymentsTotal
    /// sumando los PaymentVouchers Validated y los Payment directos de la
    /// cita. Útil para handlers que devuelven una sola cita (Get, Create,
    /// Confirm, Cancel, etc.).
    ///
    /// Para listas grandes (GetAgenda) preferir el método batch que
    /// hace UNA sola query en vez de N queries.
    /// </summary>
    public static async Task<AppointmentResponse> ToResponseAsync(
        Appointment a,
        IApplicationDbContext db,
        CancellationToken ct)
    {
        // No usamos SumAsync directo sobre Money.Amount porque está mapeado
        // con HasConversion y EF/Npgsql no lo traduce dentro de agregaciones
        // SQL. Traemos las rows planas y sumamos en C# — son pocos rows por cita.
        var vouchers = await db.PaymentVouchers
            .Where(v => v.AppointmentId == a.Id && v.Status == PaymentVoucherStatus.Validated)
            .ToListAsync(ct);
        var validatedAmount = vouchers.Sum(v => v.ReportedAmount.Amount);

        var payments = await db.Payments
            .Where(p => p.AppointmentId == a.Id)
            .ToListAsync(ct);
        var directTotal = payments.Sum(p => p.Amount.Amount);

        return ToResponse(a, validatedAmount, directTotal);
    }

    /// <summary>
    /// Batch para listas de citas: 1 sola query por categoría (vouchers
    /// validados y payments) y agrupamos en memoria. El GroupBy en SQL no
    /// se traduce por el HasConversion de Money, pero los registros de un
    /// día son ~30-100 filas a lo sumo — agregar en C# es trivial.
    /// </summary>
    public record AppointmentMoneyTotals(decimal ValidatedDeposit, decimal DirectPayments);

    public static async Task<Dictionary<Guid, AppointmentMoneyTotals>> GetMoneyTotalsAsync(
        IReadOnlyCollection<Guid> appointmentIds,
        IApplicationDbContext db,
        CancellationToken ct)
    {
        if (appointmentIds.Count == 0) return new();

        var vouchers = await db.PaymentVouchers
            .Where(v => appointmentIds.Contains(v.AppointmentId)
                     && v.Status == PaymentVoucherStatus.Validated)
            .ToListAsync(ct);
        var depositByAppt = vouchers
            .GroupBy(v => v.AppointmentId)
            .ToDictionary(g => g.Key, g => g.Sum(v => v.ReportedAmount.Amount));

        var payments = await db.Payments
            .Where(p => appointmentIds.Contains(p.AppointmentId))
            .ToListAsync(ct);
        var paidByAppt = payments
            .GroupBy(p => p.AppointmentId)
            .ToDictionary(g => g.Key, g => g.Sum(p => p.Amount.Amount));

        // Une las dos claves (puede que una cita tenga voucher pero no
        // payment, o viceversa).
        var allIds = depositByAppt.Keys.Union(paidByAppt.Keys);
        return allIds.ToDictionary(
            id => id,
            id => new AppointmentMoneyTotals(
                depositByAppt.GetValueOrDefault(id, 0m),
                paidByAppt.GetValueOrDefault(id, 0m)));
    }

    /// <summary>
    /// Compat: helper viejo que solo devolvía los validated deposits.
    /// Lo mantengo para no romper call sites existentes (GetAgenda).
    /// Internamente delega al método nuevo y extrae solo el deposit.
    /// </summary>
    public static async Task<Dictionary<Guid, decimal>> GetValidatedDepositAmountsAsync(
        IReadOnlyCollection<Guid> appointmentIds,
        IApplicationDbContext db,
        CancellationToken ct)
    {
        if (appointmentIds.Count == 0) return new Dictionary<Guid, decimal>();

        var vouchers = await db.PaymentVouchers
            .Where(v => appointmentIds.Contains(v.AppointmentId)
                     && v.Status == PaymentVoucherStatus.Validated)
            .ToListAsync(ct);

        return vouchers
            .GroupBy(v => v.AppointmentId)
            .ToDictionary(g => g.Key, g => g.Sum(v => v.ReportedAmount.Amount));
    }
}
