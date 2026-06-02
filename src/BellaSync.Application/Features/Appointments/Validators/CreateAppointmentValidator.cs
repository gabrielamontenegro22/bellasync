using BellaSync.Application.Features.Appointments.CreateAppointment;
using FluentValidation;

namespace BellaSync.Application.Features.Appointments.Validators;

public class CreateAppointmentValidator : AbstractValidator<CreateAppointmentCommand>
{
    public CreateAppointmentValidator()
    {
        RuleFor(x => x.CustomerId).NotEmpty().WithMessage("El cliente es obligatorio.");
        RuleFor(x => x.StylistId).NotEmpty().WithMessage("El estilista es obligatorio.");
        RuleFor(x => x.ServiceId).NotEmpty().WithMessage("El servicio es obligatorio.");
        RuleFor(x => x.StartAtUtc)
            .Must(d => d > DateTime.UnixEpoch).WithMessage("La fecha de inicio es obligatoria.");
        RuleFor(x => x.Notes).MaximumLength(2000);
    }
}
