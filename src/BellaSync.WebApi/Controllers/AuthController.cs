using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using BellaSync.Application.Common.Interfaces;
using BellaSync.Application.Features.Auth.Dtos;
using BellaSync.Domain.Entities;
using BellaSync.Infrastructure.Auth;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace BellaSync.WebApi.Controllers;

/// <summary>
/// Endpoints de autenticación: registro de salón nuevo y login.
/// Ambos son anónimos.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IApplicationDbContext _db;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IJwtTokenService _jwt;
    private readonly IRefreshTokenGenerator _refreshTokenGenerator;
    private readonly JwtSettings _jwtSettings;
    private readonly IValidator<RegisterSalonRequest> _registerValidator;
    private readonly IValidator<LoginRequest> _loginValidator;
    private readonly IValidator<ForgotPasswordRequest> _forgotValidator;
    private readonly IValidator<ResetPasswordRequest> _resetValidator;
    private readonly IEmailService _emailService;
    private readonly IConfiguration _configuration;
    private readonly ILogger<AuthController> _logger;

    public AuthController(
        IApplicationDbContext db,
        IPasswordHasher passwordHasher,
        IJwtTokenService jwt,
        IRefreshTokenGenerator refreshTokenGenerator,
        IOptions<JwtSettings> jwtSettings,
        IValidator<RegisterSalonRequest> registerValidator,
        IValidator<LoginRequest> loginValidator,
        IValidator<ForgotPasswordRequest> forgotValidator,
        IValidator<ResetPasswordRequest> resetValidator,
        IEmailService emailService,
        IConfiguration configuration,
        ILogger<AuthController> logger)
    {
        _db = db;
        _passwordHasher = passwordHasher;
        _jwt = jwt;
        _refreshTokenGenerator = refreshTokenGenerator;
        _jwtSettings = jwtSettings.Value;
        _registerValidator = registerValidator;
        _loginValidator = loginValidator;
        _forgotValidator = forgotValidator;
        _resetValidator = resetValidator;
        _emailService = emailService;
        _configuration = configuration;
        _logger = logger;
    }

    /// <summary>
    /// Registra un salón nuevo. Crea Tenant + usuario administrador en una operación.
    /// </summary>
    [HttpPost("register-salon")]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> RegisterSalon(
        [FromBody] RegisterSalonRequest request,
        CancellationToken ct)
    {
        var validation = await _registerValidator.ValidateAsync(request, ct);
        if (!validation.IsValid)
        {
            return ValidationProblem(BuildModelState(validation));
        }

        var normalizedEmail = request.AdminEmail.Trim().ToLowerInvariant();
        var slug = SlugGenerator.Generate(request.SalonName);

        var slugExists = await _db.Tenants
            .IgnoreQueryFilters()
            .AnyAsync(t => t.Slug == slug, ct);
        if (slugExists)
        {
            // Si el slug colisiona, le añadimos un sufijo aleatorio corto.
            slug = $"{slug}-{Guid.NewGuid().ToString("N")[..6]}";
        }

        var emailExists = await _db.Users
            .IgnoreQueryFilters()
            .AnyAsync(u => u.Email == normalizedEmail, ct);
        if (emailExists)
        {
            return Conflict(new ProblemDetails
            {
                Title = "Email ya registrado",
                Detail = "Ya existe un usuario con ese correo electrónico.",
                Status = StatusCodes.Status409Conflict
            });
        }

        var tenant = new Tenant
        {
            Id = Guid.NewGuid(),
            Name = request.SalonName.Trim(),
            Slug = slug,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        var adminUser = new User
        {
            Id = Guid.NewGuid(),
            TenantId = tenant.Id,
            Email = normalizedEmail,
            PasswordHash = _passwordHasher.Hash(request.AdminPassword),
            FullName = request.AdminFullName.Trim(),
            Role = UserRole.SalonAdmin,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        _db.Tenants.Add(tenant);
        _db.Users.Add(adminUser);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Salón {SalonName} ({TenantId}) registrado con admin {Email}",
            tenant.Name, tenant.Id, adminUser.Email);

        var response = await IssueAuthResponseAsync(adminUser, tenant, replacesTokenHash: null, ct);
        return StatusCode(StatusCodes.Status201Created, response);
    }

    /// <summary>
    /// Login con email + contraseña. Devuelve un JWT válido.
    /// </summary>
    [HttpPost("login")]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Login(
        [FromBody] LoginRequest request,
        CancellationToken ct)
    {
        var validation = await _loginValidator.ValidateAsync(request, ct);
        if (!validation.IsValid)
        {
            return ValidationProblem(BuildModelState(validation));
        }

        var normalizedEmail = request.Email.Trim().ToLowerInvariant();

        // IgnoreQueryFilters porque el login es anónimo (no hay tenant en el JWT todavía)
        var user = await _db.Users
            .IgnoreQueryFilters()
            .Include(u => u.Tenant)
            .FirstOrDefaultAsync(u => u.Email == normalizedEmail, ct);

        if (user is null || !user.IsActive)
        {
            return Unauthorized(new ProblemDetails
            {
                Title = "Credenciales inválidas",
                Status = StatusCodes.Status401Unauthorized
            });
        }

        if (user.Tenant is { IsActive: false })
        {
            return Unauthorized(new ProblemDetails
            {
                Title = "Salón inactivo",
                Detail = "El salón asociado a este usuario ha sido desactivado. Contacta al soporte.",
                Status = StatusCodes.Status401Unauthorized
            });
        }

        if (!_passwordHasher.Verify(request.Password, user.PasswordHash))
        {
            return Unauthorized(new ProblemDetails
            {
                Title = "Credenciales inválidas",
                Status = StatusCodes.Status401Unauthorized
            });
        }

        user.LastLoginAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        var response = await IssueAuthResponseAsync(user, user.Tenant, replacesTokenHash: null, ct);
        return Ok(response);
    }

    /// <summary>
    /// Rota un refresh token: lo invalida y emite uno nuevo + un nuevo access token.
    /// Endpoint anónimo (el access token está vencido al momento de llamar).
    /// Si se detecta reuse de un token revocado, se revoca toda la cadena del user.
    /// </summary>
    [HttpPost("refresh")]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Refresh(
        [FromBody] RefreshTokenRequest request,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.RefreshToken))
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Refresh token requerido",
                Status = StatusCodes.Status400BadRequest
            });
        }

        var hash = _refreshTokenGenerator.Hash(request.RefreshToken);

        var existing = await _db.RefreshTokens
            .IgnoreQueryFilters()
            .Include(t => t.User)
                .ThenInclude(u => u.Tenant)
            .FirstOrDefaultAsync(t => t.TokenHash == hash, ct);

        if (existing is null)
        {
            return Unauthorized(new ProblemDetails
            {
                Title = "Refresh token inválido",
                Status = StatusCodes.Status401Unauthorized
            });
        }

        // Detección de token reuse: si el token ya fue revocado pero alguien
        // lo intenta usar, el legítimo ya fue rotado. Indica posible robo.
        // Defensa: revocar TODOS los refresh tokens activos del user.
        if (!existing.IsActive())
        {
            _logger.LogWarning(
                "Reuse de refresh token detectado para user {UserId}. Revocando toda la cadena.",
                existing.UserId);

            var allActive = await _db.RefreshTokens
                .IgnoreQueryFilters()
                .Where(t => t.UserId == existing.UserId && t.RevokedAt == null)
                .ToListAsync(ct);
            foreach (var t in allActive) t.Revoke();
            await _db.SaveChangesAsync(ct);

            return Unauthorized(new ProblemDetails
            {
                Title = "Refresh token inválido",
                Detail = "Token revocado. Por seguridad se cerraron todas las sesiones.",
                Status = StatusCodes.Status401Unauthorized
            });
        }

        // Validar que el user/tenant sigan activos
        if (!existing.User.IsActive || existing.User.Tenant is { IsActive: false })
        {
            return Unauthorized(new ProblemDetails
            {
                Title = "Sesión inválida",
                Detail = "La cuenta ya no está activa.",
                Status = StatusCodes.Status401Unauthorized
            });
        }

        // Rotar: revocar el actual y emitir uno nuevo
        var response = await IssueAuthResponseAsync(
            existing.User,
            existing.User.Tenant,
            replacesTokenHash: hash,
            ct);

        existing.Revoke(replacedByHash: _refreshTokenGenerator.Hash(response.RefreshToken));
        await _db.SaveChangesAsync(ct);

        return Ok(response);
    }

    /// <summary>
    /// Solicita el envío de un enlace de reseteo de contraseña.
    /// Siempre responde 200 OK aunque el email no exista (no revelar enumeration).
    /// </summary>
    [HttpPost("forgot-password")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ForgotPassword(
        [FromBody] ForgotPasswordRequest request,
        CancellationToken ct)
    {
        var validation = await _forgotValidator.ValidateAsync(request, ct);
        if (!validation.IsValid)
        {
            return ValidationProblem(BuildModelState(validation));
        }

        var normalizedEmail = request.Email.Trim().ToLowerInvariant();

        var user = await _db.Users
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.Email == normalizedEmail, ct);

        if (user is null || !user.IsActive)
        {
            _logger.LogInformation(
                "Forgot password solicitado para email no existente o inactivo: {Email}",
                normalizedEmail);
            return Ok();
        }

        // Invalidar tokens previos no usados del mismo usuario.
        // Previene la situación donde alguien tiene N tokens válidos
        // simultáneos por haber pedido forgot-password varias veces.
        // Solo el más reciente queda activo.
        var now = DateTime.UtcNow;
        var previousActive = await _db.PasswordResetTokens
            .Where(t => t.UserId == user.Id && t.UsedAt == null && t.ExpiresAt > now)
            .ToListAsync(ct);
        foreach (var prev in previousActive)
        {
            prev.UsedAt = now;
        }

        // Generar token hex de 64 chars (32 bytes random)
        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32))
            .ToLowerInvariant();

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

        return Ok();
    }

    /// <summary>
    /// Guarda la nueva contraseña usando un token recibido por email.
    /// El token es de un solo uso (se invalida con UsedAt) y expira a la hora.
    /// </summary>
    [HttpPost("reset-password")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ResetPassword(
        [FromBody] ResetPasswordRequest request,
        CancellationToken ct)
    {
        var validation = await _resetValidator.ValidateAsync(request, ct);
        if (!validation.IsValid)
        {
            return ValidationProblem(BuildModelState(validation));
        }

        // Include User + Tenant para revalidar IsActive de ambos en el momento
        // del uso del token (no solo cuando se solicitó). Cubre la ventana
        // entre "solicitar reset" y "consumir reset".
        var entity = await _db.PasswordResetTokens
            .IgnoreQueryFilters()
            .Include(t => t.User)
                .ThenInclude(u => u.Tenant)
            .FirstOrDefaultAsync(t => t.Token == request.Token, ct);

        if (entity is null || entity.UsedAt.HasValue || entity.ExpiresAt < DateTime.UtcNow)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Token inválido",
                Detail = "El enlace expiró o ya fue usado. Solicita uno nuevo.",
                Status = StatusCodes.Status400BadRequest
            });
        }

        // Revalidar que el usuario y su tenant sigan activos en el momento de uso.
        // Si fueron desactivados entre solicitar y usar el token, rechazar.
        // Mensaje genérico para no diferenciar entre "tenant inactivo" y
        // "usuario inactivo" — no es información que necesite el cliente.
        if (!entity.User.IsActive || entity.User.Tenant is { IsActive: false })
        {
            _logger.LogWarning(
                "Reset password rechazado por user/tenant inactivo: user={UserId} tenant={TenantId}",
                entity.User.Id, entity.User.TenantId);
            return BadRequest(new ProblemDetails
            {
                Title = "Token inválido",
                Detail = "El enlace ya no es válido. Contacta al soporte si crees que es un error.",
                Status = StatusCodes.Status400BadRequest
            });
        }

        entity.User.PasswordHash = _passwordHasher.Hash(request.NewPassword);
        entity.UsedAt = DateTime.UtcNow;

        // Cerrar #7c: al cambiar password, revocar TODOS los refresh tokens
        // activos del user. Las sesiones existentes seguirán funcionando con
        // su access token actual (corto: 15-30 min) y al expirar no podrán
        // refrescar. Esto limita la ventana de exposición si la cuenta fue
        // comprometida.
        var activeRefreshTokens = await _db.RefreshTokens
            .Where(t => t.UserId == entity.UserId && t.RevokedAt == null)
            .ToListAsync(ct);
        foreach (var rt in activeRefreshTokens) rt.Revoke();

        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Password reset completado para {Email}. Revocados {Count} refresh tokens.",
            entity.User.Email, activeRefreshTokens.Count);
        return NoContent();
    }

    /// <summary>
    /// Helper: genera access + refresh tokens, persiste el refresh, y devuelve
    /// la AuthResponse completa. Reutilizado por register, login y refresh.
    /// </summary>
    private async Task<AuthResponse> IssueAuthResponseAsync(
        User user,
        Tenant? tenant,
        string? replacesTokenHash,
        CancellationToken ct)
    {
        var (accessToken, accessExpiresAt) = _jwt.GenerateToken(user);

        var (refreshPlaintext, refreshHash) = _refreshTokenGenerator.Generate();
        var refreshExpiresAt = DateTime.UtcNow.AddDays(_jwtSettings.RefreshTokenDays);

        var refresh = RefreshToken.Create(
            userId: user.Id,
            tokenHash: refreshHash,
            expiresAtUtc: refreshExpiresAt,
            createdByIp: HttpContext.Connection.RemoteIpAddress?.ToString(),
            replacesTokenHash: replacesTokenHash);

        _db.RefreshTokens.Add(refresh);
        await _db.SaveChangesAsync(ct);

        return new AuthResponse
        {
            Token = accessToken,
            ExpiresAtUtc = accessExpiresAt,
            RefreshToken = refreshPlaintext,
            RefreshTokenExpiresAtUtc = refreshExpiresAt,
            UserId = user.Id,
            Email = user.Email,
            FullName = user.FullName,
            Role = user.Role.ToString(),
            TenantId = user.TenantId,
            TenantName = tenant?.Name ?? string.Empty,
            TenantSlug = tenant?.Slug ?? string.Empty,
        };
    }

    private static Microsoft.AspNetCore.Mvc.ModelBinding.ModelStateDictionary BuildModelState(
        FluentValidation.Results.ValidationResult result)
    {
        var modelState = new Microsoft.AspNetCore.Mvc.ModelBinding.ModelStateDictionary();
        foreach (var error in result.Errors)
        {
            modelState.AddModelError(error.PropertyName, error.ErrorMessage);
        }
        return modelState;
    }
}

/// <summary>
/// Genera slugs URL-friendly a partir del nombre del salón.
/// "Bella Spa Neiva" -> "bella-spa-neiva"
/// </summary>
internal static class SlugGenerator
{
    public static string Generate(string source)
    {
        if (string.IsNullOrWhiteSpace(source)) return Guid.NewGuid().ToString("N")[..8];

        // Quitar diacríticos
        var normalized = source.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(normalized.Length);
        foreach (var ch in normalized)
        {
            var category = CharUnicodeInfo.GetUnicodeCategory(ch);
            if (category != UnicodeCategory.NonSpacingMark)
                sb.Append(ch);
        }

        var ascii = sb.ToString().Normalize(NormalizationForm.FormC);
        // Reemplazar caracteres no alfanuméricos por guiones
        var slug = Regex.Replace(ascii, @"[^a-z0-9]+", "-").Trim('-');
        return string.IsNullOrEmpty(slug) ? Guid.NewGuid().ToString("N")[..8] : slug;
    }
}
