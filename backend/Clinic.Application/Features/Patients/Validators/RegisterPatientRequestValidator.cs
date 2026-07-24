using Clinic.Application.Features.Patients.DTOs;
using Clinic.Domain.Constants;
using Clinic.Domain.Enums;
using FluentValidation;

namespace Clinic.Application.Features.Patients.Validators;

public class RegisterPatientRequestValidator : AbstractValidator<RegisterPatientRequest>
{
    public RegisterPatientRequestValidator()
    {
        RuleFor(x => x.FirstName)
            .NotEmpty().WithMessage("First name is required.")
            .MaximumLength(100);

        RuleFor(x => x.LastName)
            .NotEmpty().WithMessage("Last name is required.")
            .MaximumLength(100);

        RuleFor(x => x.Phone)
            .NotEmpty().WithMessage("Phone number is required.")
            .MaximumLength(20)
            .Matches(@"^\+?[0-9 ()\-]{7,20}$")
            .WithMessage("Enter a valid phone number (digits, spaces, +, -, parentheses).");

        RuleFor(x => x.Email)
            .EmailAddress().WithMessage("Enter a valid email address.")
            .MaximumLength(256)
            .When(x => !string.IsNullOrWhiteSpace(x.Email));

        RuleFor(x => x.Address)
            .MaximumLength(500);

        RuleFor(x => x.Gender)
            .NotEmpty().WithMessage("Gender is required.")
            .Must(g => Enum.TryParse<Gender>(g, true, out _))
            .WithMessage($"Gender must be one of: {string.Join(", ", Enum.GetNames<Gender>())}.");

        RuleFor(x => x.DateOfBirth)
            .LessThanOrEqualTo(_ => DateOnly.FromDateTime(DateTime.UtcNow))
            .WithMessage("Date of birth cannot be in the future.")
            .When(x => x.DateOfBirth.HasValue);

        RuleFor(x => x.BloodGroup)
            .Must(bg => BloodGroups.IsValid(bg!))
            .WithMessage($"Blood group must be one of: {string.Join(", ", BloodGroups.All)}.")
            .When(x => !string.IsNullOrWhiteSpace(x.BloodGroup));

        RuleForEach(x => x.MedicalConditionCodes)
            .NotEmpty().WithMessage("Medical condition codes cannot be empty.");
    }
}
