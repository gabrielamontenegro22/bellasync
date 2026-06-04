using BellaSync.Application.Common.Handlers;
using BellaSync.Application.Features.Payments.Dtos;
using BellaSync.Domain.Entities;

namespace BellaSync.Application.Features.Payments.RegisterPayment;

/// <summary>
/// Registra que se recibió un pago de una cita. NO procesa el pago
/// (efectivo, transferencia o tarjeta se manejan por fuera) — solo
/// queda el registro contable para reportería.
///
/// AppointmentId va en la URL del endpoint y el controller lo monta acá.
/// </summary>
public sealed record RegisterPaymentCommand(
    Guid AppointmentId,
    PaymentMethod Method,
    /// <summary>
    /// Banco/billetera (Transfer) o marca (Card). Obligatorio para
    /// Transfer; opcional para Card; debe ser null para Cash.
    /// </summary>
    string? Provider,
    decimal Amount,
    decimal Tip,
    string? Reference,
    /// <summary>
    /// Quién registra el pago. El controller lo extrae del JWT (User.FindFirst sub).
    /// null permitido para tests / scripts internos.
    /// </summary>
    Guid? RegisteredByUserId
) : ICommand<PaymentResponse>;
