namespace BellaSync.Application.Common.Interfaces;

/// <summary>
/// Abstracción para envío de emails transaccionales.
/// En desarrollo se usa LoggingEmailService (loguea con Serilog).
/// En producción se reemplaza por SendGrid / Postmark / Mailgun / etc.
/// </summary>
public interface IEmailService
{
    /// <summary>
    /// Envía un email con el enlace de reseteo de contraseña.
    /// </summary>
    /// <param name="toEmail">Email del destinatario.</param>
    /// <param name="fullName">Nombre completo del usuario (para el saludo).</param>
    /// <param name="resetUrl">URL completa con token (ej. http://localhost:5173/reset-password?token=xxx).</param>
    Task SendPasswordResetAsync(
        string toEmail,
        string fullName,
        string resetUrl,
        CancellationToken cancellationToken = default);
}
