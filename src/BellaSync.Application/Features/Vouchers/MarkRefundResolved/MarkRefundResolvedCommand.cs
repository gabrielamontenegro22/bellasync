using BellaSync.Application.Common.Handlers;
using BellaSync.Application.Features.Vouchers.Dtos;

namespace BellaSync.Application.Features.Vouchers.MarkRefundResolved;

/// <summary>
/// La admin marca un refund pendiente como resuelto — típicamente
/// después de hacer la transferencia bancaria por fuera del sistema.
/// Aplica tanto para Refunded (devolución hecha) como para CreditPending
/// (se aplicó el crédito a una nueva cita).
///
/// Solo admin lo puede invocar (el endpoint exige rol). Idempotente: si
/// el voucher ya estaba resuelto, devuelve el snapshot actual sin
/// reescribir RefundResolvedAt para preservar la fecha real.
/// </summary>
public sealed record MarkRefundResolvedCommand(Guid VoucherId)
    : ICommand<PendingRefundResponse>;
