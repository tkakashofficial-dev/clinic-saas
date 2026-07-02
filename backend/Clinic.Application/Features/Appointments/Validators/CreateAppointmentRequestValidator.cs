using Clinic.Application.Features.Appointments.DTOs;
using FluentValidation;

namespace Clinic.Application.Features.Appointments.Validators;

public class CreateAppointmentRequestValidator : AbstractValidator<CreateAppointmentRequest>
{
    public CreateAppointmentRequestValidator()
    {
        RuleFor(x => x.PatientId)
            .NotEmpty().WithMessage("Patient is required.");

        RuleFor(x => x.DoctorTenantUserId)
            .NotEmpty().WithMessage("Doctor is required.");

        // Small grace window so "book for right now" doesn't fail on clock skew
        RuleFor(x => x.AppointmentDate)
            .GreaterThan(_ => DateTime.UtcNow.AddMinutes(-5))
            .WithMessage("Appointment date cannot be in the past.");

        RuleFor(x => x.Notes)
            .MaximumLength(1000);
    }
}
