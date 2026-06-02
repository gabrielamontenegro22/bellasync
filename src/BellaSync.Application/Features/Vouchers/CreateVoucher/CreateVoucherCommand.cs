using BellaSync.Application.Common.Handlers;
using BellaSync.Application.Features.Vouchers.Dtos;

namespace BellaSync.Application.Features.Vouchers.CreateVoucher;

/// <summary>
/// Crea un voucher para una cita. Lo invocará el webhook de WhatsApp en el
/// futuro; mientras tanto, también puede llamarse desde recepción para
/// registrar comprobantes recibidos por otros canales (email, audio, etc.).
/// </summary>
public sealed record CreateVoucherCommand(
    Guid AppointmentId,
    decimal ReportedAmount,
    string? Bank,
    string? ReferenceNumber,
    string? SenderName,
    string? SenderPhone,
    string? ImageUrl) : ICommand<VoucherResponse>;
