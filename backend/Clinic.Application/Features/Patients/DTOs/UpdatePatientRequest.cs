namespace Clinic.Application.Features.Patients.DTOs;

public class UpdatePatientRequest
{
    public string FirstName { get; set; } = default!;
    public string LastName { get; set; } = default!;
    public string Phone { get; set; } = default!;
    public string? Email { get; set; }
    public string? Address { get; set; }
    public string Gender { get; set; } = default!;
    public DateOnly? DateOfBirth { get; set; }
    public string? BloodGroup { get; set; }

    /// <summary>Full replacement set — the edit form sends every checked code,
    /// and the service syncs the join table to match exactly.</summary>
    public List<string> MedicalConditionCodes { get; set; } = new();
}
