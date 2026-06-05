using BellaSync.Application.Common.Handlers;
using BellaSync.Application.Features.Vouchers.Dtos;

namespace BellaSync.Application.Features.Vouchers.GetCustomerCredits;

/// <summary>
/// Lista los créditos disponibles de un cliente — vouchers Validated con
/// RefundDecision = CreditPending que todavía tienen saldo (no se aplicaron
/// completos a otra cita y no se cerraron por la admin desde Caja).
///
/// El frontend lo llama al elegir cliente en NewAppointmentModal para
/// detectar si ofrece aplicar crédito.
/// </summary>
public sealed record GetCustomerCreditsQuery(Guid CustomerId)
    : IQuery<IReadOnlyList<CustomerCreditResponse>>;
