using BellaSync.Application.Common.Handlers;
using BellaSync.Application.Features.Vouchers.Dtos;

namespace BellaSync.Application.Features.Vouchers.GetPendingRefunds;

/// <summary>
/// Lista de devoluciones pendientes que la admin todavía debe accionar
/// — el anticipo se decidió devolver (Refunded) o aplicar a próxima cita
/// (CreditPending), pero nadie marcó la transferencia/aplicación como
/// hecha. Sin filtros — todos del tenant que estén abiertos.
/// </summary>
public sealed record GetPendingRefundsQuery() : IQuery<IReadOnlyList<PendingRefundResponse>>;
