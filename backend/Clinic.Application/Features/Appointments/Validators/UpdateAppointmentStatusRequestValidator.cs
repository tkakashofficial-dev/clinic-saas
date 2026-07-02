using Clinic.Application.Features.Appointments.DTOs;
using Clinic.Domain.Enums;
using FluentValidation;

namespace Clinic.Application.Features.Appointments.Validators;

public class UpdateAppointmentStatusRequestValidator : AbstractValidator<UpdateAppointmentStatusRequest>
{
    public UpdateAppointmentStatusRequestValidator()
    {
        RuleFor(x => x.Status)
            .NotEmpty().WithMessage("Status is required.")
            .Must(s => Enum.TryParse<AppointmentStatus>(s, true, out _))
            .WithMessage($"Status must be one of: {string.Join(", ", Enum.GetNames<AppointmentStatus>())}.");
    }
}
