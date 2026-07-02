namespace Clinic.Application.Features.Consultations.DTOs;

public class ConsultationDto
{
    public Guid Id { get; set; }
    public Guid AppointmentId { get; set; }
    public string Diagnosis { get; set; } = default!;
    public string? TreatmentNotes { get; set; }
    public string? BloodPressure { get; set; }
    public int? PulseBpm { get; set; }
    public decimal? TemperatureCelsius { get; set; }
    public decimal? WeightKg { get; set; }
    public string DoctorName { get; set; } = default!;
    public DateTime RecordedAt { get; set; }
    public PrescriptionDto? Prescription { get; set; }
}

public class PrescriptionDto
{
    public Guid Id { get; set; }
    public string? Notes { get; set; }
    public List<PrescriptionItemDto> Items { get; set; } = new();
}

public class PrescriptionItemDto
{
    public string MedicineName { get; set; } = default!;
    public string? Dosage { get; set; }
    public string? Frequency { get; set; }
    public int? DurationDays { get; set; }
    public string? Instructions { get; set; }
}
