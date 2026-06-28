namespace Clinic.Domain.Entities;

// Join table — which conditions does this patient have
// Example: Patient X has Diabetes + Hypertension
public class PatientMedicalCondition
{
    public Guid PatientId { get; private set; }
    public Guid MedicalConditionId { get; private set; }
    public DateTime RecordedAt { get; private set; } = DateTime.UtcNow;
    public string? Notes { get; private set; }

    public Patient Patient { get; private set; } = default!;
    public MedicalCondition MedicalCondition { get; private set; } = default!;

    private PatientMedicalCondition() { }

    public PatientMedicalCondition(Guid patientId, Guid medicalConditionId, string? notes = null)
    {
        PatientId = patientId;
        MedicalConditionId = medicalConditionId;
        Notes = notes;
    }
}