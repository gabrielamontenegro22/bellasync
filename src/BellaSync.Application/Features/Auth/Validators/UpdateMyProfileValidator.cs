using BellaSync.Application.Features.Auth.Dtos;
using FluentValidation;

namespace BellaSync.Application.Features.Auth.Validators;

public class UpdateMyProfileValidator : AbstractValidator<UpdateMyProfileRequest>
{
    public UpdateMyProfileValidator()
    {
        RuleFor(x => x.FullName)
            .NotEmpty().WithMessage("El nombre completo es obligatorio.")
            .MinimumLength(2).WithMessage("El nombre debe tener al menos 2 caracteres.")
            .MaximumLength(120).WithMessage("El nombre es demasiado largo.");
    }
}
