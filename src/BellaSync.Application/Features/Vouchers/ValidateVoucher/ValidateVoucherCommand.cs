using BellaSync.Application.Common.Handlers;
using BellaSync.Application.Features.Vouchers.Dtos;

namespace BellaSync.Application.Features.Vouchers.ValidateVoucher;

/// <summary>
/// La recepción decide sobre un voucher. Si confirm, la cita asociada
/// pasa a Confirmed. Si reject/clarification, la cita queda en Pending
/// y la recepción notifica al cliente fuera del sistema.
/// </summary>
public sealed record ValidateVoucherCommand(
    Guid VoucherId,
    VoucherDecision Decision,
    Guid DecidedByUserId,
    string? Notes) : ICommand<VoucherResponse>;

public enum VoucherDecision
{
    Confirm = 0,
    Reject = 1,
    RequestClarification = 2,
}
