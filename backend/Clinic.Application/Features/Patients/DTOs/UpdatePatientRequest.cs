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
}
