namespace Clinic.Domain.Entities;

/// <summary>One medicine line on a prescription.</summary>
public class PrescriptionItem
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid PrescriptionId { get; private set; }

    public string MedicineName { get; private set; } = default!;
    public string? Dosage { get; private set; }        // e.g. "500 mg"
    public string? Frequency { get; private set; }     // e.g. "3x daily after meals"
    public int? DurationDays { get; private set; }
    public string? Instructions { get; private set; }

    public Prescription Prescription { get; private set; } = default!;

    private PrescriptionItem() { }

    public PrescriptionItem(
        Guid prescriptionId,
        string medicineName,
        string? dosage = null,
        string? frequency = null,
        int? durationDays = null,
        string? instructions = null)
    {
        PrescriptionId = prescriptionId;
        MedicineName = medicineName ?? throw new ArgumentNullException(nameof(medicineName));
        Dosage = dosage;
        Frequency = frequency;
        DurationDays = durationDays;
        Instructions = instructions;
    }
}
