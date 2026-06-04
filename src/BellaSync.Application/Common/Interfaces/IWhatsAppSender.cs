namespace BellaSync.Application.Common.Interfaces;

/// <summary>
/// Adapter para mandar mensajes WhatsApp. Implementaciones:
///
///   - NoOpWhatsAppSender: loguea pero no envía (default mientras no
///     hay cuenta Meta/Twilio configurada). Devuelve éxito siempre.
///   - TwilioWhatsAppSender: vía Twilio Programmable Messaging.
///   - MetaWhatsAppSender: vía Meta WhatsApp Cloud API directo.
///
/// La capa Application solo conoce IWhatsAppSender — cambiar de
/// proveedor es swap del binding en DI sin tocar handlers ni dispatcher.
///
/// El dispatcher llama SendAsync y según el WhatsAppSendResult marca el
/// WhatsAppMessage como Sent (con ExternalMessageId) o Failed (con
/// FailureReason). No tira excepciones — los errores del provider los
/// envuelve en SendResult.Failure para que el dispatcher pueda persistir
/// el motivo sin try/catch.
/// </summary>
public interface IWhatsAppSender
{
    Task<WhatsAppSendResult> SendAsync(
        string toPhone,
        string body,
        CancellationToken ct = default);
}

/// <summary>
/// Resultado del intento de envío. Discriminated-union ligero:
///   - IsSuccess + ExternalMessageId cuando el provider aceptó
///   - !IsSuccess + FailureReason cuando rechazó
/// </summary>
public sealed record WhatsAppSendResult(
    bool IsSuccess,
    string? ExternalMessageId,
    string? FailureReason)
{
    public static WhatsAppSendResult Success(string? externalMessageId)
        => new(true, externalMessageId, null);

    public static WhatsAppSendResult Failure(string reason)
        => new(false, null, reason);
}
