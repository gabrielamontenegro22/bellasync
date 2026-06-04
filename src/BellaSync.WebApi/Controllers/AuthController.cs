using BellaSync.Application.Auth;
using BellaSync.Application.Common.Handlers;
using BellaSync.Application.Common.Interfaces;
using BellaSync.Application.Common.Results;
using BellaSync.Application.Features.Auth.ChangeMyPassword;
using BellaSync.Application.Features.Auth.Dtos;
using BellaSync.Application.Features.Auth.ForgotPassword;
using BellaSync.Application.Features.Auth.Login;
using BellaSync.Application.Features.Auth.MyProfile;
using BellaSync.Application.Features.Auth.RefreshAccessToken;
using BellaSync.Application.Features.Auth.RegisterSalon;
using BellaSync.Application.Features.Auth.ResetPassword;
using BellaSync.WebApi.Infrastructure;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace BellaSync.WebApi.Controllers;

/// <summary>
/// Endpoints de autenticación. Controller delgado: cada acción valida con
/// FluentValidation, mapea el DTO a un Command/Query y despacha al handler.
/// Todos los endpoints son anónimos.
///
/// REFRESH TOKEN COMO COOKIE HTTPONLY:
/// register/login/refresh setean el refresh token en una cookie HttpOnly
/// (inalcanzable desde JavaScript → mitiga XSS). El body de respuesta sigue
/// incluyendo el refreshToken para compatibilidad con clientes legacy.
/// El endpoint /refresh prefiere la cookie sobre el body si ambos existen.
/// /logout borra la cookie.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    /// <summary>Nombre de la cookie HttpOnly que guarda el refresh token.</summary>
    public const string RefreshCookieName = "bellasync_refresh";

    private readonly IClock _clock;
    private readonly JwtSettings _jwtSettings;
    private readonly IHostEnvironment _env;

    public AuthController(
        IClock clock,
        IOptions<JwtSettings> jwtSettings,
        IHostEnvironment env)
    {
        _clock = clock;
        _jwtSettings = jwtSettings.Value;
        _env = env;
    }

    private string? RemoteIp => HttpContext.Connection.RemoteIpAddress?.ToString();

    [HttpPost("register-salon")]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> RegisterSalon(
        [FromBody] RegisterSalonRequest request,
        [FromServices] IValidator<RegisterSalonRequest> validator,
        [FromServices] ICommandHandler<RegisterSalonCommand, AuthResponse> handler,
        CancellationToken ct)
    {
        var validation = await validator.ValidateAsync(request, ct);
        if (!validation.IsValid)
            return ValidationProblem(BuildModelState(validation));

        var command = new RegisterSalonCommand(
            request.SalonName,
            request.AdminFullName,
            request.AdminEmail,
            request.AdminPassword,
            RemoteIp);

        var result = await handler.HandleAsync(command, ct);
        SetRefreshCookieIfSuccess(result);
        return result.ToActionResult();
    }

    [HttpPost("login")]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Login(
        [FromBody] LoginRequest request,
        [FromServices] IValidator<LoginRequest> validator,
        [FromServices] ICommandHandler<LoginCommand, AuthResponse> handler,
        CancellationToken ct)
    {
        var validation = await validator.ValidateAsync(request, ct);
        if (!validation.IsValid)
            return ValidationProblem(BuildModelState(validation));

        var result = await handler.HandleAsync(
            new LoginCommand(request.Email, request.Password, RemoteIp), ct);
        SetRefreshCookieIfSuccess(result);
        return result.ToActionResult();
    }

    /// <summary>
    /// Rota un refresh token. Lee el token de:
    ///  1. Cookie HttpOnly "bellasync_refresh" (preferido)
    ///  2. Body { refreshToken } como fallback (clientes legacy)
    ///
    /// Setea la nueva cookie con el refresh rotado.
    /// Detección de reuse: si se usa un token revocado, revoca toda la cadena.
    /// </summary>
    [HttpPost("refresh")]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Refresh(
        [FromBody] RefreshTokenRequest? request,
        [FromServices] ICommandHandler<RefreshAccessTokenCommand, AuthResponse> handler,
        CancellationToken ct)
    {
        // Prefer cookie sobre body. Si ninguno → 400.
        var tokenFromCookie = Request.Cookies[RefreshCookieName];
        var refreshToken = !string.IsNullOrWhiteSpace(tokenFromCookie)
            ? tokenFromCookie
            : request?.RefreshToken;

        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Refresh token requerido",
                Detail = "No se encontró refresh token ni en la cookie ni en el body.",
                Status = StatusCodes.Status400BadRequest,
            });
        }

        var result = await handler.HandleAsync(
            new RefreshAccessTokenCommand(refreshToken, RemoteIp), ct);

        // Si el refresh falla, borrar la cookie para que el cliente no quede
        // con un cookie inválido pegado (el handler revoca la cadena en reuse).
        if (result.IsFailure) ClearRefreshCookie();
        else SetRefreshCookieIfSuccess(result);

        return result.ToActionResult();
    }

    [HttpPost("forgot-password")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ForgotPassword(
        [FromBody] ForgotPasswordRequest request,
        [FromServices] IValidator<ForgotPasswordRequest> validator,
        [FromServices] ICommandHandler<ForgotPasswordCommand> handler,
        CancellationToken ct)
    {
        var validation = await validator.ValidateAsync(request, ct);
        if (!validation.IsValid)
            return ValidationProblem(BuildModelState(validation));

        var result = await handler.HandleAsync(new ForgotPasswordCommand(request.Email), ct);
        return result.ToActionResult();
    }

    [HttpPost("reset-password")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ResetPassword(
        [FromBody] ResetPasswordRequest request,
        [FromServices] IValidator<ResetPasswordRequest> validator,
        [FromServices] ICommandHandler<ResetPasswordCommand> handler,
        CancellationToken ct)
    {
        var validation = await validator.ValidateAsync(request, ct);
        if (!validation.IsValid)
            return ValidationProblem(BuildModelState(validation));

        var result = await handler.HandleAsync(
            new ResetPasswordCommand(request.Token, request.NewPassword), ct);

        // El reset revoca todos los refresh tokens → la cookie ya no sirve.
        if (result.IsSuccess) ClearRefreshCookie();

        return result.ToActionResult();
    }

    /// <summary>
    /// Cierra sesión: revoca el refresh token actual (si está en la cookie)
    /// y borra la cookie. Siempre devuelve 204 — idempotente.
    /// </summary>
    [HttpPost("logout")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Logout(
        [FromServices] IApplicationDbContext db,
        [FromServices] Application.Features.Auth.Shared.AuthTokenIssuer tokenIssuer,
        CancellationToken ct)
    {
        var cookieToken = Request.Cookies[RefreshCookieName];
        if (!string.IsNullOrWhiteSpace(cookieToken))
        {
            var hash = tokenIssuer.HashRefreshToken(cookieToken);
            var existing = await db.RefreshTokens
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(t => t.TokenHash == hash, ct);
            if (existing is not null && existing.RevokedAt is null)
            {
                existing.Revoke(_clock.UtcNow);
                await db.SaveChangesAsync(ct);
            }
        }

        ClearRefreshCookie();
        return NoContent();
    }

    // ===== Mi cuenta (endpoints autenticados) =====

    /// <summary>
    /// Devuelve los datos del user logueado actual (nombre, email, rol,
    /// nombre del salón). Lo consume la página /mi-cuenta del frontend.
    /// </summary>
    [HttpGet("me")]
    [Authorize]
    [ProducesResponseType(typeof(MyProfileResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetMyProfile(
        [FromServices] IQueryHandler<GetMyProfileQuery, MyProfileResponse> handler,
        CancellationToken ct)
    {
        var result = await handler.HandleAsync(new GetMyProfileQuery(), ct);
        return result.ToActionResult();
    }

    /// <summary>
    /// Actualiza el perfil del user logueado. Por ahora solo nombre.
    /// Cambio de email queda fuera de scope (requiere flujo de verificación).
    /// </summary>
    [HttpPut("me")]
    [Authorize]
    [ProducesResponseType(typeof(MyProfileResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> UpdateMyProfile(
        [FromBody] UpdateMyProfileRequest request,
        [FromServices] IValidator<UpdateMyProfileRequest> validator,
        [FromServices] ICommandHandler<UpdateMyProfileCommand, MyProfileResponse> handler,
        CancellationToken ct)
    {
        var validation = await validator.ValidateAsync(request, ct);
        if (!validation.IsValid)
            return ValidationProblem(BuildModelState(validation));

        var result = await handler.HandleAsync(
            new UpdateMyProfileCommand(request.FullName), ct);
        return result.ToActionResult();
    }

    /// <summary>
    /// Cambia la contraseña del user logueado. Verifica la actual antes
    /// de aceptar. Revoca refresh tokens en otros dispositivos.
    /// La sesión actual sigue viva hasta que expire el access token.
    /// </summary>
    [HttpPost("change-password")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> ChangeMyPassword(
        [FromBody] ChangeMyPasswordRequest request,
        [FromServices] IValidator<ChangeMyPasswordRequest> validator,
        [FromServices] ICommandHandler<ChangeMyPasswordCommand> handler,
        CancellationToken ct)
    {
        var validation = await validator.ValidateAsync(request, ct);
        if (!validation.IsValid)
            return ValidationProblem(BuildModelState(validation));

        var result = await handler.HandleAsync(
            new ChangeMyPasswordCommand(request.CurrentPassword, request.NewPassword), ct);
        return result.ToActionResult();
    }

    // ===== Helpers privados de cookie =====

    private void SetRefreshCookieIfSuccess(Result<AuthResponse> result)
    {
        if (result.IsFailure || result.Value is null) return;

        Response.Cookies.Append(
            RefreshCookieName,
            result.Value.RefreshToken,
            BuildCookieOptions(expiresAt: result.Value.RefreshTokenExpiresAtUtc));
    }

    private void ClearRefreshCookie()
    {
        // Para borrar una cookie hay que setearla con MaxAge=0 + mismas opciones
        // de Path/SameSite/Secure que se usaron al crearla.
        Response.Cookies.Delete(RefreshCookieName, BuildCookieOptions(expiresAt: null));
    }

    private CookieOptions BuildCookieOptions(DateTime? expiresAt)
    {
        return new CookieOptions
        {
            HttpOnly = true,
            // En producción exigimos HTTPS. En desarrollo (HTTP) Secure debe
            // ser false o el browser ignora la cookie.
            Secure = !_env.IsDevelopment(),
            // Lax: la cookie se envía en navegación top-level y same-site
            // POST. Frontend va a /api via Vite proxy (mismo origen).
            SameSite = SameSiteMode.Lax,
            // Path mínimo: la cookie solo se envía a endpoints de auth.
            Path = "/api/Auth",
            Expires = expiresAt,
        };
    }

    private static Microsoft.AspNetCore.Mvc.ModelBinding.ModelStateDictionary BuildModelState(
        FluentValidation.Results.ValidationResult result)
    {
        var modelState = new Microsoft.AspNetCore.Mvc.ModelBinding.ModelStateDictionary();
        foreach (var error in result.Errors)
            modelState.AddModelError(error.PropertyName, error.ErrorMessage);
        return modelState;
    }
}
