namespace Clinic.Application.Features.Consultations.DTOs;

public class RecordConsultationRequest
{
    public string Diagnosis { get; set; } = default!;
    public string? TreatmentNotes { get; set; }

    /// <summary>Optional — not every consultation ends with medicines.</summary>
    public PrescriptionRequest? Prescription { get; set; }
}

public class PrescriptionRequest
{
    public string? Notes { get; set; }
    public List<PrescriptionItemRequest> Items { get; set; } = new();
}

public class PrescriptionItemRequest
{
    public string MedicineName { get; set; } = default!;
    public string? Dosage { get; set; }
    public string? Frequency { get; set; }
    public int? DurationDays { get; set; }
    public string? Instructions { get; set; }
}
