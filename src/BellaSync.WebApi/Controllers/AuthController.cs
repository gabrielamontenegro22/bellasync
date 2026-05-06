using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using BellaSync.Application.Common.Interfaces;
using BellaSync.Application.Features.Auth.Dtos;
using BellaSync.Domain.Entities;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

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
    private readonly IValidator<RegisterSalonRequest> _registerValidator;
    private readonly IValidator<LoginRequest> _loginValidator;
    private readonly ILogger<AuthController> _logger;

    public AuthController(
        IApplicationDbContext db,
        IPasswordHasher passwordHasher,
        IJwtTokenService jwt,
        IValidator<RegisterSalonRequest> registerValidator,
        IValidator<LoginRequest> loginValidator,
        ILogger<AuthController> logger)
    {
        _db = db;
        _passwordHasher = passwordHasher;
        _jwt = jwt;
        _registerValidator = registerValidator;
        _loginValidator = loginValidator;
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

        var (token, expiresAt) = _jwt.GenerateToken(adminUser);

        var response = new AuthResponse
        {
            Token = token,
            ExpiresAtUtc = expiresAt,
            UserId = adminUser.Id,
            Email = adminUser.Email,
            FullName = adminUser.FullName,
            Role = adminUser.Role.ToString(),
            TenantId = tenant.Id,
            TenantName = tenant.Name,
            TenantSlug = tenant.Slug
        };

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

        var (token, expiresAt) = _jwt.GenerateToken(user);

        return Ok(new AuthResponse
        {
            Token = token,
            ExpiresAtUtc = expiresAt,
            UserId = user.Id,
            Email = user.Email,
            FullName = user.FullName,
            Role = user.Role.ToString(),
            TenantId = user.TenantId,
            TenantName = user.Tenant?.Name ?? string.Empty,
            TenantSlug = user.Tenant?.Slug ?? string.Empty
        });
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
