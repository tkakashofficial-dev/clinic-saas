using Clinic.Application.Features.Auth.DTOs;
using FluentValidation;

namespace Clinic.Application.Features.Auth.Validators;

public class CreateClinicRequestValidator : AbstractValidator<CreateClinicRequest>
{
    public CreateClinicRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Clinic name is required.")
            .MaximumLength(200);
    }
}

public class SwitchClinicRequestValidator : AbstractValidator<SwitchClinicRequest>
{
    public SwitchClinicRequestValidator()
    {
        RuleFor(x => x.TenantId)
            .NotEmpty().WithMessage("Clinic is required.");
    }
}
