using Clinic.Application.Features.Consultations.DTOs;
using FluentValidation;

namespace Clinic.Application.Features.Consultations.Validators;

public class RecordConsultationRequestValidator : AbstractValidator<RecordConsultationRequest>
{
    public RecordConsultationRequestValidator()
    {
        RuleFor(x => x.Diagnosis)
            .NotEmpty().WithMessage("Diagnosis is required.")
            .MaximumLength(2000);

        RuleFor(x => x.TreatmentNotes)
            .MaximumLength(2000);

        When(x => x.Prescription is not null, () =>
        {
            RuleFor(x => x.Prescription!.Notes)
                .MaximumLength(1000);

            RuleFor(x => x.Prescription!.Items)
                .NotEmpty().WithMessage("A prescription must contain at least one medicine.");

            RuleForEach(x => x.Prescription!.Items).ChildRules(item =>
            {
                item.RuleFor(i => i.MedicineName)
                    .NotEmpty().WithMessage("Medicine name is required.")
                    .MaximumLength(200);
                item.RuleFor(i => i.Dosage).MaximumLength(100);
                item.RuleFor(i => i.Frequency).MaximumLength(100);
                item.RuleFor(i => i.Instructions).MaximumLength(500);
                item.RuleFor(i => i.DurationDays)
                    .GreaterThan(0).When(i => i.DurationDays.HasValue)
                    .WithMessage("Duration must be at least 1 day.");
            });
        });
    }
}
