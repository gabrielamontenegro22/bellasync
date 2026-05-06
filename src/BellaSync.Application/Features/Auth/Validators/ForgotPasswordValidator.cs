using BellaSync.Application.Features.Auth.Dtos;
using FluentValidation;

namespace BellaSync.Application.Features.Auth.Validators;

public class ForgotPasswordValidator : AbstractValidator<ForgotPasswordRequest>
{
    public ForgotPasswordValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("El correo electrónico es obligatorio.")
            .EmailAddress().WithMessage("Formato de correo electrónico inválido.")
            .MaximumLength(150);
    }
}
