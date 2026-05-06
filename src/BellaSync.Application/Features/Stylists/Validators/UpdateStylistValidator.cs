using BellaSync.Application.Features.Stylists.Dtos;
using FluentValidation;

namespace BellaSync.Application.Features.Stylists.Validators;

public class UpdateStylistValidator : AbstractValidator<UpdateStylistRequest>
{
    public UpdateStylistValidator()
    {
        RuleFor(x => x.FullName)
            .NotEmpty().WithMessage("El nombre del estilista es obligatorio.")
            .MinimumLength(StylistValidationRules.FullNameMinLength)
            .WithMessage($"El nombre debe tener al menos {StylistValidationRules.FullNameMinLength} caracteres.")
            .MaximumLength(StylistValidationRules.FullNameMaxLength);

        RuleFor(x => x.Phone)
            .Must(p => string.IsNullOrEmpty(p) || StylistValidationRules.PhoneRegex.IsMatch(p))
            .WithMessage("El teléfono no es válido.")
            .MaximumLength(StylistValidationRules.PhoneMaxLength);

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
