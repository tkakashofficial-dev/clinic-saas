namespace Clinic.Application.Features.Patients.DTOs;

/// <summary>Everything a doctor wants before the patient sits down.</summary>
public class PatientHistoryDto
{
    public PatientDto Patient { get; set; } = default!;
    public List<PatientConsultationDto> Consultations { get; set; } = new();
}

public class PatientConsultationDto
{
    public Guid ConsultationId { get; set; }
    public DateTime AppointmentDate { get; set; }
    public DateTime RecordedAt { get; set; }
    public string DoctorName { get; set; } = default!;
    public string Diagnosis { get; set; } = default!;
    public string? TreatmentNotes { get; set; }
    public string? BloodPressure { get; set; }
    public int? PulseBpm { get; set; }
    public decimal? TemperatureCelsius { get; set; }
    public decimal? WeightKg { get; set; }
    public Guid? PrescriptionId { get; set; }
}
