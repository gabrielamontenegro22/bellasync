using BellaSync.Application.Common.Interfaces;
using Microsoft.Extensions.Logging;

namespace BellaSync.Infrastructure.Services;

/// <summary>
/// Implementación de IEmailService para desarrollo.
/// NO envía emails reales; loguea el contenido con Serilog para que la
/// dev pueda copiar el reset URL desde la consola del backend.
/// En producción se reemplaza por SendGridEmailService (a implementar).
/// </summary>
public class LoggingEmailService : IEmailService
{
    private readonly ILogger<LoggingEmailService> _logger;

    public LoggingEmailService(ILogger<LoggingEmailService> logger)
    {
        _logger = logger;
    }

    public Task SendPasswordResetAsync(
        string toEmail,
        string fullName,
        string resetUrl,
        CancellationToken cancellationToken = default)
    {
        _logger.LogWarning(
            @"
================================================================
  PASSWORD RESET (DEV MODE — no se envió email real)
  Para:    {Email}  ({Name})
  URL:     {Url}
  Asunto:  Restablece tu contraseña en BellaSync
================================================================",
            toEmail, fullName, resetUrl);

        return Task.CompletedTask;
    }
}
