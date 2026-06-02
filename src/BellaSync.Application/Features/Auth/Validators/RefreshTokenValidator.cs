using BellaSync.Application.Features.Auth.Dtos;
using FluentValidation;

namespace BellaSync.Application.Features.Auth.Validators;

public class RefreshTokenValidator : AbstractValidator<RefreshTokenRequest>
{
    public RefreshTokenValidator()
    {
        RuleFor(x => x.RefreshToken)
            .NotEmpty().WithMessage("Refresh token requerido.")
            .MinimumLength(32).WithMessage("Refresh token con longitud inválida.")
            .MaximumLength(256).WithMessage("Refresh token con longitud inválida.");
    }
}
