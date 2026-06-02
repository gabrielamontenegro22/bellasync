using BellaSync.Application.Features.Appointments.CreatePublicAppointment;
using FluentValidation;

namespace BellaSync.Application.Features.Appointments.Validators;

public class CreatePublicAppointmentValidator : AbstractValidator<CreatePublicAppointmentCommand>
{
    public CreatePublicAppointmentValidator()
    {
        RuleFor(x => x.TenantSlug)
            .NotEmpty().WithMessage("El salón es obligatorio.")
            .MaximumLength(120);

        RuleFor(x => x.StylistId).NotEmpty().WithMessage("El estilista es obligatorio.");
        RuleFor(x => x.ServiceId).NotEmpty().WithMessage("El servicio es obligatorio.");

        RuleFor(x => x.StartAtUtc)
            .Must(d => d > DateTime.UnixEpoch).WithMessage("La fecha de inicio es obligatoria.");

        RuleFor(x => x.ClientName)
            .NotEmpty().WithMessage("El nombre es obligatorio.")
            .MinimumLength(3)
            .MaximumLength(150);

        RuleFor(x => x.ClientPhone)
            .NotEmpty().WithMessage("El teléfono es obligatorio.")
            .MaximumLength(30);

        RuleFor(x => x.ClientEmail)
            .EmailAddress().When(x => !string.IsNullOrWhiteSpace(x.ClientEmail))
            .MaximumLength(150);
    }
}
