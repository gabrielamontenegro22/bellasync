using BellaSync.Application.Features.Auth.Dtos;
using FluentValidation;

namespace BellaSync.Application.Features.Auth.Validators;

public class ResetPasswordValidator : AbstractValidator<ResetPasswordRequest>
{
    public ResetPasswordValidator()
    {
        RuleFor(x => x.Token)
            .NotEmpty().WithMessage("Token requerido.")
            .Length(32, 128).WithMessage("Token con longitud inválida.");

        RuleFor(x => x.NewPassword)
            .NotEmpty().WithMessage("La contraseña es obligatoria.")
            .MinimumLength(8).WithMessage("La contraseña debe tener al menos 8 caracteres.")
            .MaximumLength(100)
            .Matches("[A-Z]").WithMessage("La contraseña debe incluir al menos una letra mayúscula.")
            .Matches("[a-z]").WithMessage("La contraseña debe incluir al menos una letra minúscula.")
            .Matches("[0-9]").WithMessage("La contraseña debe incluir al menos un número.");
    }
}
