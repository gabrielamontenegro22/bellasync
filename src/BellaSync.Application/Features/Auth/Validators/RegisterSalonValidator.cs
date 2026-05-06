using BellaSync.Application.Features.Auth.Dtos;
using FluentValidation;

namespace BellaSync.Application.Features.Auth.Validators;

public class RegisterSalonValidator : AbstractValidator<RegisterSalonRequest>
{
    public RegisterSalonValidator()
    {
        RuleFor(x => x.SalonName)
            .NotEmpty().WithMessage("El nombre del salón es obligatorio.")
            .MinimumLength(3).WithMessage("El nombre del salón debe tener al menos 3 caracteres.")
            .MaximumLength(100).WithMessage("El nombre del salón no puede superar los 100 caracteres.");

        RuleFor(x => x.AdminFullName)
            .NotEmpty().WithMessage("El nombre del administrador es obligatorio.")
            .MinimumLength(3).WithMessage("El nombre del administrador debe tener al menos 3 caracteres.")
            .MaximumLength(150);

        RuleFor(x => x.AdminEmail)
            .NotEmpty().WithMessage("El correo electrónico es obligatorio.")
            .EmailAddress().WithMessage("Formato de correo electrónico inválido.")
            .MaximumLength(150);

        RuleFor(x => x.AdminPassword)
            .NotEmpty().WithMessage("La contraseña es obligatoria.")
            .MinimumLength(8).WithMessage("La contraseña debe tener al menos 8 caracteres.")
            .MaximumLength(100)
            .Matches("[A-Z]").WithMessage("La contraseña debe incluir al menos una letra mayúscula.")
            .Matches("[a-z]").WithMessage("La contraseña debe incluir al menos una letra minúscula.")
            .Matches("[0-9]").WithMessage("La contraseña debe incluir al menos un número.");
    }
}
