using BellaSync.Application.Features.Customers.Dtos;
using FluentValidation;

namespace BellaSync.Application.Features.Customers.Validators;

public class CreateCustomerValidator : AbstractValidator<CreateCustomerRequest>
{
    public CreateCustomerValidator()
    {
        RuleFor(x => x.FullName)
            .NotEmpty().WithMessage("El nombre del cliente es obligatorio.")
            .MinimumLength(CustomerValidationRules.FullNameMinLength)
            .WithMessage($"El nombre debe tener al menos {CustomerValidationRules.FullNameMinLength} caracteres.")
            .MaximumLength(CustomerValidationRules.FullNameMaxLength);

        RuleFor(x => x.Phone)
            .NotEmpty().WithMessage("El teléfono es obligatorio.")
            .Matches(CustomerValidationRules.PhoneRegex).WithMessage("El teléfono no es válido.")
            .MaximumLength(CustomerValidationRules.PhoneMaxLength);

        RuleFor(x => x.Email)
            .EmailAddress().When(x => !string.IsNullOrWhiteSpace(x.Email))
            .WithMessage("El correo electrónico no es válido.")
            .MaximumLength(CustomerValidationRules.EmailMaxLength);

        RuleFor(x => x.Birthday)
            .Must(d => !d.HasValue || d.Value <= DateOnly.FromDateTime(DateTime.UtcNow))
            .WithMessage("La fecha de nacimiento no puede ser futura.")
            .Must(d => !d.HasValue || d.Value.Year >= 1900)
            .WithMessage("Fecha de nacimiento inválida.");

        RuleFor(x => x.DocumentNumber)
            .MaximumLength(CustomerValidationRules.DocumentMaxLength);

        RuleFor(x => x.Address)
            .MaximumLength(CustomerValidationRules.AddressMaxLength);

        RuleFor(x => x.Notes)
            .MaximumLength(CustomerValidationRules.NotesMaxLength);
    }
}
