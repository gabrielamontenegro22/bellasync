using BellaSync.Application.Common.Handlers;
using BellaSync.Application.Features.Auth.Dtos;
using BellaSync.Application.Features.Auth.ForgotPassword;
using BellaSync.Application.Features.Auth.Login;
using BellaSync.Application.Features.Auth.RefreshAccessToken;
using BellaSync.Application.Features.Auth.RegisterSalon;
using BellaSync.Application.Features.Auth.ResetPassword;
using BellaSync.WebApi.Infrastructure;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;

namespace BellaSync.WebApi.Controllers;

/// <summary>
/// Endpoints de autenticación. Controller delgado: cada acción valida con
/// FluentValidation, mapea el DTO a un Command/Query y despacha al handler.
/// Todos los endpoints son anónimos.
///
/// Nota: register-salon ahora devuelve 200 OK (era 201). Como no hay endpoint
/// GET /salons/{id} al cual apuntar Location, no aplicaba el contrato
/// estándar de CreatedAtAction. El cuerpo (AuthResponse) es idéntico.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
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
        return result.ToActionResult();
    }

    /// <summary>
    /// Rota un refresh token: lo invalida y emite uno nuevo + un nuevo access.
    /// Detección de reuse: si se usa un token revocado, se revoca toda la cadena.
    /// </summary>
    [HttpPost("refresh")]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Refresh(
        [FromBody] RefreshTokenRequest request,
        [FromServices] IValidator<RefreshTokenRequest> validator,
        [FromServices] ICommandHandler<RefreshAccessTokenCommand, AuthResponse> handler,
        CancellationToken ct)
    {
        var validation = await validator.ValidateAsync(request, ct);
        if (!validation.IsValid)
            return ValidationProblem(BuildModelState(validation));

        var result = await handler.HandleAsync(
            new RefreshAccessTokenCommand(request.RefreshToken, RemoteIp), ct);
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
        return result.ToActionResult();
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
