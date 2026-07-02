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

        // Vitals: loose but sane clinical ranges
        RuleFor(x => x.BloodPressure)
            .MaximumLength(20)
            .Matches(@"^\d{2,3}\s*/\s*\d{2,3}$").WithMessage("Blood pressure looks like 120/80.")
            .When(x => !string.IsNullOrWhiteSpace(x.BloodPressure));

        RuleFor(x => x.PulseBpm)
            .InclusiveBetween(20, 250).When(x => x.PulseBpm.HasValue);

        RuleFor(x => x.TemperatureCelsius)
            .InclusiveBetween(30, 45).WithMessage("Temperature is in °C (30–45).")
            .When(x => x.TemperatureCelsius.HasValue);

        RuleFor(x => x.WeightKg)
            .InclusiveBetween(0.5m, 500).When(x => x.WeightKg.HasValue);

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
