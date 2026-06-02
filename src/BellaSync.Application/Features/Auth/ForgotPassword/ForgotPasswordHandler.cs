using System.Security.Cryptography;
using BellaSync.Application.Common.Handlers;
using BellaSync.Application.Common.Interfaces;
using BellaSync.Application.Common.Results;
using BellaSync.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace BellaSync.Application.Features.Auth.ForgotPassword;

/// <summary>
/// Solicita el envío de un enlace de reset por email.
/// SIEMPRE devuelve Success aunque el email no exista (no revelar enumeration).
/// Invalida tokens previos no usados del mismo user (solo uno activo a la vez).
/// </summary>
public sealed class ForgotPasswordHandler : ICommandHandler<ForgotPasswordCommand>
{
    private readonly IApplicationDbContext _db;
    private readonly IEmailService _emailService;
    private readonly IConfiguration _configuration;
    private readonly ILogger<ForgotPasswordHandler> _logger;

    public ForgotPasswordHandler(
        IApplicationDbContext db,
        IEmailService emailService,
        IConfiguration configuration,
        ILogger<ForgotPasswordHandler> logger)
    {
        _db = db;
        _emailService = emailService;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<Result> HandleAsync(ForgotPasswordCommand command, CancellationToken ct)
    {
        var normalizedEmail = command.Email.Trim().ToLowerInvariant();

        var user = await _db.Users
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.Email == normalizedEmail, ct);

        if (user is null || !user.IsActive)
        {
            _logger.LogInformation(
                "Forgot password solicitado para email no existente o inactivo: {Email}",
                normalizedEmail);
            return Result.Success();
        }

        // Invalidar tokens previos no usados — solo el más reciente queda activo.
        var now = DateTime.UtcNow;
        var previousActive = await _db.PasswordResetTokens
            .Where(t => t.UserId == user.Id && t.UsedAt == null && t.ExpiresAt > now)
            .ToListAsync(ct);
        foreach (var prev in previousActive) prev.UsedAt = now;

        // Token hex 64 chars (32 bytes random crypto)
        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();

        var entity = new PasswordResetToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            Token = token,
            ExpiresAt = now.AddHours(1),
            CreatedAt = now,
        };
        _db.PasswordResetTokens.Add(entity);
        await _db.SaveChangesAsync(ct);

        if (previousActive.Count > 0)
        {
            _logger.LogInformation(
                "Forgot password: invalidados {Count} tokens previos del usuario {Email}",
                previousActive.Count, user.Email);
        }

        var frontendBaseUrl = _configuration["Frontend:BaseUrl"] ?? "http://localhost:5173";
        var resetUrl = $"{frontendBaseUrl.TrimEnd('/')}/reset-password?token={token}";

        await _emailService.SendPasswordResetAsync(user.Email, user.FullName, resetUrl, ct);

        return Result.Success();
    }
}
