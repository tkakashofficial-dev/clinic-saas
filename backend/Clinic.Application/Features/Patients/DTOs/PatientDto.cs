namespace Clinic.Application.Features.Patients.DTOs;

public class PatientDto
{
    public Guid Id { get; set; }
    public string FullName { get; set; } = default!;
    public string Phone { get; set; } = default!;
    public string? Email { get; set; }
    public string? Address { get; set; }
    public string Gender { get; set; } = default!;
    public DateOnly? DateOfBirth { get; set; }
    public int? Age { get; set; }
    public List<string> MedicalConditions { get; set; } = new();
    public DateTime RegisteredAt { get; set; }
}