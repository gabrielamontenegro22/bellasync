using BellaSync.Application.Features.Stylists.Dtos;
using FluentValidation;

namespace BellaSync.Application.Features.Stylists.Validators;

public class CreateStylistValidator : AbstractValidator<CreateStylistRequest>
{
    public CreateStylistValidator()
    {
        RuleFor(x => x.FullName)
            .NotEmpty().WithMessage("El nombre del estilista es obligatorio.")
            .MinimumLength(StylistValidationRules.FullNameMinLength)
            .WithMessage($"El nombre debe tener al menos {StylistValidationRules.FullNameMinLength} caracteres.")
            .MaximumLength(StylistValidationRules.FullNameMaxLength);

        RuleFor(x => x.Role)
            .NotEmpty().WithMessage("El cargo es obligatorio.")
            .MaximumLength(80).WithMessage("El cargo no puede superar los 80 caracteres.");

        RuleFor(x => x.Email)
            .EmailAddress().WithMessage("Formato de correo electrónico inválido.")
            .MaximumLength(150)
            .When(x => !string.IsNullOrWhiteSpace(x.Email));

        RuleFor(x => x.Phone)
            .Must(p => string.IsNullOrEmpty(p) || StylistValidationRules.PhoneRegex.IsMatch(p))
            .WithMessage("El teléfono no es válido.")
            .MaximumLength(StylistValidationRules.PhoneMaxLength);

        RuleFor(x => x.IdNumber)
            .MaximumLength(30).WithMessage("La cédula no puede superar los 30 caracteres.");

        RuleFor(x => x.Color)
            .Must(c => string.IsNullOrEmpty(c) || StylistValidationRules.HexColorRegex.IsMatch(c))
            .WithMessage("El color debe estar en formato hexadecimal (#RRGGBB o #RGB).");

        RuleFor(x => x.HireDate)
            .Must(d => !d.HasValue || d.Value <= DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)))
            .WithMessage("La fecha de ingreso no puede ser futura.");

        RuleFor(x => x.ServiceIds)
            .Must(ids => ids.Distinct().Count() == ids.Count)
            .WithMessage("La lista de servicios contiene ids duplicados.");
    }
}
