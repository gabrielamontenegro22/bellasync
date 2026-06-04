using BellaSync.Application.Features.Auth.Dtos;
using FluentValidation;

namespace BellaSync.Application.Features.Auth.Validators;

/// <summary>
/// Mismas reglas de robustez que ResetPassword para la nueva contraseña
/// (8+ chars, mayúscula, minúscula, número). La actual solo se chequea
/// no-vacía — la verificación real va contra el hash en el handler.
/// </summary>
public class ChangeMyPasswordValidator : AbstractValidator<ChangeMyPasswordRequest>
{
    public ChangeMyPasswordValidator()
    {
        RuleFor(x => x.CurrentPassword)
            .NotEmpty().WithMessage("Tenés que escribir tu contraseña actual.");

        RuleFor(x => x.NewPassword)
            .NotEmpty().WithMessage("La contraseña nueva es obligatoria.")
            .MinimumLength(8).WithMessage("La contraseña debe tener al menos 8 caracteres.")
            .MaximumLength(100)
            .Matches("[A-Z]").WithMessage("La contraseña debe incluir al menos una letra mayúscula.")
            .Matches("[a-z]").WithMessage("La contraseña debe incluir al menos una letra minúscula.")
            .Matches("[0-9]").WithMessage("La contraseña debe incluir al menos un número.");
    }
}
