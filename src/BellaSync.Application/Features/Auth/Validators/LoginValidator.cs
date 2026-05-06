using BellaSync.Application.Features.Auth.Dtos;
using FluentValidation;

namespace BellaSync.Application.Features.Auth.Validators;

public class LoginValidator : AbstractValidator<LoginRequest>
{
    public LoginValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("El correo electrónico es obligatorio.")
            .EmailAddress().WithMessage("Formato de correo electrónico inválido.");

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("La contraseña es obligatoria.");
    }
}
