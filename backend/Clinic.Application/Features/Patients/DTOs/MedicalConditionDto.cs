namespace Clinic.Application.Features.Patients.DTOs;

/// <summary>One seeded medical condition — rendered as a tick-box on the
/// patient form. Code is the stable API value, Name is what staff read.</summary>
public class MedicalConditionDto
{
    public string Name { get; set; } = default!;
    public string Code { get; set; } = default!;
}
