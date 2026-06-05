using System.Net.Http.Json;
using System.Text.Json.Serialization;
using BellaSync.Application.Common.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BellaSync.Infrastructure.Services;

/// <summary>
/// Implementación de IEmailService usando Resend (https://resend.com).
///
/// Por qué Resend vs alternativas:
///  - API REST simple (no necesita SDK): un POST a /emails con JSON.
///  - Free tier generoso (3000/mes, 100/día) — suficiente para
///    arranque y la mayoría de salones que no mandan blast marketing.
///  - Buen deliverability (no termina en spam si configurás SPF/DKIM
///    del dominio "bellasync.app").
///
/// Configuración requerida en appsettings (ver EmailSettings):
///   "Email": {
///     "Provider": "Resend",
///     "FromAddress": "no-reply@bellasync.app",
///     "FromName": "BellaSync",
///     "Resend": { "ApiKey": "re_xxxxx" }
///   }
///
/// Si la API key falta, DependencyInjection cae a LoggingEmailService
/// para que dev no se rompa. En producción la admin de BellaSync setea
/// la key via variable de entorno (Email__Resend__ApiKey).
/// </summary>
public sealed class ResendEmailService : IEmailService
{
    private readonly HttpClient _http;
    private readonly EmailSettings _settings;
    private readonly ILogger<ResendEmailService> _logger;

    private const string ResendEndpoint = "https://api.resend.com/emails";

    public ResendEmailService(
        HttpClient http,
        IOptions<EmailSettings> settings,
        ILogger<ResendEmailService> logger)
    {
        _http = http;
        _settings = settings.Value;
        _logger = logger;

        if (string.IsNullOrWhiteSpace(_settings.Resend?.ApiKey))
            throw new InvalidOperationException(
                "ResendEmailService requiere Email:Resend:ApiKey configurada. " +
                "Si querés usar el logger de dev, configurá Email:Provider=Logging.");

        _http.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue(
                "Bearer", _settings.Resend.ApiKey);
    }

    public async Task SendPasswordResetAsync(
        string toEmail,
        string fullName,
        string resetUrl,
        CancellationToken cancellationToken = default)
    {
        var fromAddress = _settings.FromAddress ?? "no-reply@bellasync.app";
        var fromName = _settings.FromName ?? "BellaSync";

        var firstName = (fullName ?? string.Empty).Split(' ').FirstOrDefault() ?? "Hola";

        var payload = new ResendEmailRequest(
            From: $"{fromName} <{fromAddress}>",
            To: new[] { toEmail },
            Subject: "Restablece tu contraseña en BellaSync",
            Html: BuildPasswordResetHtml(firstName, resetUrl),
            Text: BuildPasswordResetText(firstName, resetUrl));

        try
        {
            using var response = await _http.PostAsJsonAsync(ResendEndpoint, payload, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogError(
                    "Resend devolvió {Status} al enviar reset password a {Email}. Body: {Body}",
                    (int)response.StatusCode, toEmail, body);
                // No lanzamos excepción — el endpoint /forgot-password siempre
                // responde 200 al usuario para no filtrar si el email existe.
                // El error queda registrado para investigar.
                return;
            }

            _logger.LogInformation(
                "Reset password email enviado a {Email} vía Resend.", toEmail);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex,
                "Falló la conexión con Resend al enviar reset password a {Email}.", toEmail);
            // Tampoco lanzamos — mismo motivo de privacidad.
        }
    }

    // ────────────────────────────────────────────────────────────────
    // Templates de email
    // ────────────────────────────────────────────────────────────────

    /// <summary>
    /// HTML branded simple — colores y tipografía BellaSync.
    /// Inline styles porque la mayoría de clientes de email no soportan
    /// &lt;style&gt; en el head ni clases externas.
    /// </summary>
    private static string BuildPasswordResetHtml(string firstName, string resetUrl) => $@"
<!DOCTYPE html>
<html lang=""es"">
<head>
  <meta charset=""utf-8"" />
  <meta name=""viewport"" content=""width=device-width,initial-scale=1"" />
  <title>Restablece tu contraseña</title>
</head>
<body style=""margin:0;padding:0;background:#faf8f5;font-family:-apple-system,BlinkMacSystemFont,'Segoe UI',Roboto,sans-serif;color:#2e2b25;"">
  <table role=""presentation"" cellpadding=""0"" cellspacing=""0"" border=""0"" width=""100%"" style=""padding:32px 16px;"">
    <tr>
      <td align=""center"">
        <table role=""presentation"" cellpadding=""0"" cellspacing=""0"" border=""0"" width=""560"" style=""max-width:560px;background:#ffffff;border-radius:14px;overflow:hidden;border:1px solid #ece7df;"">
          <!-- Brand bar -->
          <tr>
            <td style=""padding:24px 32px;border-bottom:1px solid #ece7df;"">
              <div style=""display:flex;align-items:center;gap:10px;"">
                <div style=""width:32px;height:32px;border-radius:8px;background:#0f766e;color:white;display:inline-block;text-align:center;line-height:32px;font-family:Georgia,serif;font-size:18px;font-weight:600;"">B</div>
                <span style=""font-family:Georgia,serif;font-size:22px;color:#2e2b25;letter-spacing:-0.01em;"">BellaSync</span>
              </div>
            </td>
          </tr>
          <!-- Body -->
          <tr>
            <td style=""padding:32px;"">
              <h1 style=""font-family:Georgia,serif;font-size:26px;color:#2e2b25;margin:0 0 12px;line-height:1.2;font-weight:600;"">
                Hola, {System.Net.WebUtility.HtmlEncode(firstName)}
              </h1>
              <p style=""color:#5f5a4f;font-size:15px;line-height:1.5;margin:0 0 24px;"">
                Recibimos una solicitud para restablecer la contraseña de tu cuenta en BellaSync.
                Si fuiste vos, hacé click en el botón de abajo para crear una nueva contraseña.
                El link es válido por <strong>1 hora</strong>.
              </p>
              <table role=""presentation"" cellpadding=""0"" cellspacing=""0"" border=""0"" align=""left"" style=""margin:8px 0 24px;"">
                <tr>
                  <td style=""background:#0f766e;border-radius:10px;"">
                    <a href=""{resetUrl}"" style=""display:inline-block;padding:12px 24px;color:#ffffff;text-decoration:none;font-size:14px;font-weight:500;"">
                      Restablecer mi contraseña →
                    </a>
                  </td>
                </tr>
              </table>
              <p style=""color:#80796a;font-size:12.5px;line-height:1.5;margin:0 0 8px;"">
                Si el botón no funciona, copiá y pegá este link en tu navegador:
              </p>
              <p style=""color:#0f766e;font-size:12.5px;line-height:1.5;margin:0 0 24px;word-break:break-all;"">
                <a href=""{resetUrl}"" style=""color:#0f766e;text-decoration:underline;"">{resetUrl}</a>
              </p>
              <hr style=""border:none;border-top:1px solid #ece7df;margin:24px 0;"" />
              <p style=""color:#80796a;font-size:12px;line-height:1.5;margin:0;"">
                Si no pediste restablecer tu contraseña, podés ignorar este email.
                Tu cuenta está segura — nadie cambió nada todavía.
              </p>
            </td>
          </tr>
          <!-- Footer -->
          <tr>
            <td style=""padding:20px 32px;background:#faf8f5;border-top:1px solid #ece7df;"">
              <p style=""color:#a89f8e;font-size:11.5px;line-height:1.4;margin:0;text-align:center;"">
                BellaSync · Software de gestión para salones de belleza
              </p>
            </td>
          </tr>
        </table>
      </td>
    </tr>
  </table>
</body>
</html>";

    /// <summary>
    /// Versión texto plano (fallback para clientes que no renderizan HTML).
    /// </summary>
    private static string BuildPasswordResetText(string firstName, string resetUrl) => $@"
Hola, {firstName}

Recibimos una solicitud para restablecer la contraseña de tu cuenta en BellaSync.
Si fuiste vos, abrí este link para crear una nueva contraseña (válido por 1 hora):

{resetUrl}

Si no pediste restablecer tu contraseña, podés ignorar este email.

—
BellaSync · Software de gestión para salones de belleza
".Trim();

    // ────────────────────────────────────────────────────────────────
    // DTO interno para serializar al request de Resend
    // ────────────────────────────────────────────────────────────────

    private sealed record ResendEmailRequest(
        [property: JsonPropertyName("from")] string From,
        [property: JsonPropertyName("to")] string[] To,
        [property: JsonPropertyName("subject")] string Subject,
        [property: JsonPropertyName("html")] string Html,
        [property: JsonPropertyName("text")] string Text);
}

/// <summary>
/// Configuración tipada del módulo Email. Lee de la sección "Email" de
/// appsettings (o sobrescribible por env vars como Email__Resend__ApiKey).
/// </summary>
public sealed class EmailSettings
{
    public const string SectionName = "Email";

    /// <summary>"Resend" o "Logging". Default Logging (no manda emails reales).</summary>
    public string Provider { get; set; } = "Logging";

    /// <summary>From address ej. "no-reply@bellasync.app". Debe estar verificado en Resend.</summary>
    public string? FromAddress { get; set; }

    /// <summary>Nombre que aparece como remitente ej. "BellaSync".</summary>
    public string? FromName { get; set; }

    public ResendSettings? Resend { get; set; }

    public sealed class ResendSettings
    {
        /// <summary>API key de Resend (re_xxxxx). NUNCA hardcodear acá — vía env var.</summary>
        public string? ApiKey { get; set; }
    }
}
