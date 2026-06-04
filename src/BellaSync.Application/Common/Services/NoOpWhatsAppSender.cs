using BellaSync.Application.Common.Interfaces;
using Microsoft.Extensions.Logging;

namespace BellaSync.Application.Common.Services;

/// <summary>
/// Implementación default de IWhatsAppSender que NO envía nada.
/// Loguea el mensaje y devuelve éxito con un ID sintético.
///
/// Útil porque:
///   - En dev no hace falta cuenta Twilio/Meta — el dispatcher corre,
///     los mensajes se marcan como Sent, podemos verificar end-to-end
///     que el flujo completo funciona.
///   - En staging/early-customers podemos demostrar el feature sin pagar
///     por mensajes ni configurar la cuenta.
///   - El día que conectamos un provider real, el cambio es swap de DI:
///     services.AddScoped&lt;IWhatsAppSender, TwilioWhatsAppSender&gt;().
///
/// Los logs llevan tag "[WHATSAPP-NOOP]" para que sea trivial encontrarlos
/// en producción y validar que cosa SÍ se hubiera mandado.
/// </summary>
public sealed class NoOpWhatsAppSender : IWhatsAppSender
{
    private readonly ILogger<NoOpWhatsAppSender> _logger;

    public NoOpWhatsAppSender(ILogger<NoOpWhatsAppSender> logger)
    {
        _logger = logger;
    }

    public Task<WhatsAppSendResult> SendAsync(
        string toPhone,
        string body,
        CancellationToken ct = default)
    {
        _logger.LogInformation(
            "[WHATSAPP-NOOP] would send to {Phone}: {Body}",
            toPhone, body);

        // ID sintético con prefijo claro para distinguirlo de los reales.
        var fakeId = $"noop-{Guid.NewGuid():N}";
        return Task.FromResult(WhatsAppSendResult.Success(fakeId));
    }
}
