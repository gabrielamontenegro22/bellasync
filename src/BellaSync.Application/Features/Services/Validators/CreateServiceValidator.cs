using BellaSync.Application.Features.Services.Dtos;
using FluentValidation;

namespace BellaSync.Application.Features.Services.Validators;

public class CreateServiceValidator : AbstractValidator<CreateServiceRequest>
{
    public CreateServiceValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("El nombre del servicio es obligatorio.")
            .MinimumLength(2).WithMessage("El nombre debe tener al menos 2 caracteres.")
            .MaximumLength(ServiceValidationRules.NameMaxLength)
            .WithMessage($"El nombre no puede superar los {ServiceValidationRules.NameMaxLength} caracteres.");

        RuleFor(x => x.Description)
            .MaximumLength(ServiceValidationRules.DescriptionMaxLength)
            .WithMessage($"La descripción no puede superar los {ServiceValidationRules.DescriptionMaxLength} caracteres.");

        RuleFor(x => x.Category)
            .IsInEnum().WithMessage("Categoría inválida.");

        RuleFor(x => x.DurationMinutes)
            .InclusiveBetween(ServiceValidationRules.DurationMin, ServiceValidationRules.DurationMax)
            .WithMessage($"La duración debe estar entre {ServiceValidationRules.DurationMin} y {ServiceValidationRules.DurationMax} minutos.");

        RuleFor(x => x.Price)
            .InclusiveBetween(ServiceValidationRules.PriceMin, ServiceValidationRules.PriceMax)
            .WithMessage($"El precio debe estar entre {ServiceValidationRules.PriceMin:N0} y {ServiceValidationRules.PriceMax:N0} COP.");

        RuleFor(x => x.CommissionPercentage)
            .InclusiveBetween(0m, 100m)
            .WithMessage("La comisión debe estar entre 0 y 100 por ciento.");

        RuleFor(x => x.Color)
            .Must(c => string.IsNullOrEmpty(c) || ServiceValidationRules.HexColorRegex.IsMatch(c))
            .WithMessage("El color debe estar en formato hexadecimal (#RRGGBB o #RGB).");
    }
}
