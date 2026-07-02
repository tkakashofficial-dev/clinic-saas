using Clinic.Application.Features.Staff.DTOs;
using Clinic.Domain.Constants;
using FluentValidation;

namespace Clinic.Application.Features.Staff.Validators;

public class AddStaffRequestValidator : AbstractValidator<AddStaffRequest>
{
    public AddStaffRequestValidator()
    {
        RuleFor(x => x.FirstName)
            .NotEmpty().WithMessage("First name is required.")
            .MaximumLength(100);

        RuleFor(x => x.LastName)
            .NotEmpty().WithMessage("Last name is required.")
            .MaximumLength(100);

        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email is required.")
            .EmailAddress().WithMessage("Enter a valid email address.")
            .MaximumLength(256);

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Password is required.")
            .MinimumLength(8).WithMessage("Password must be at least 8 characters.")
            .Matches("[a-zA-Z]").WithMessage("Password must contain a letter.")
            .Matches("[0-9]").WithMessage("Password must contain a digit.");

        RuleFor(x => x.Roles)
            .NotEmpty().WithMessage("At least one role is required.");

        RuleForEach(x => x.Roles)
            .Must(r => RoleNames.TryNormalize(r, out _))
            .WithMessage($"Roles must be from: {string.Join(", ", RoleNames.All)}.");
    }
}
