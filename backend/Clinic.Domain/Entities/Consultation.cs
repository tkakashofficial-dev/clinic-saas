using Clinic.Domain.Common;

namespace Clinic.Domain.Entities;

/// <summary>
/// The clinical record of what happened during an appointment: one per
/// appointment, written by the doctor. The Appointment is the booking;
/// the Consultation is the medical outcome.
/// </summary>
public class Consultation : BaseEntity, IMustHaveTenant
{
    public Guid TenantId { get; private set; }
    public Guid AppointmentId { get; private set; }
    public Guid DoctorTenantUserId { get; private set; }

    public string Diagnosis { get; private set; } = default!;
    public string? TreatmentNotes { get; private set; }

    // Vitals captured at the visit (all optional)
    public string? BloodPressure { get; private set; }   // e.g. "120/80"
    public int? PulseBpm { get; private set; }
    public decimal? TemperatureCelsius { get; private set; }
    public decimal? WeightKg { get; private set; }

    public Appointment Appointment { get; private set; } = default!;
    public TenantUser Doctor { get; private set; } = default!;
    public Prescription? Prescription { get; private set; }

    private Consultation() { }

    public Consultation(
        Guid tenantId,
        Guid appointmentId,
        Guid doctorTenantUserId,
        string diagnosis,
        string? treatmentNotes = null)
    {
        TenantId = tenantId;
        AppointmentId = appointmentId;
        DoctorTenantUserId = doctorTenantUserId;
        Diagnosis = diagnosis ?? throw new ArgumentNullException(nameof(diagnosis));
        TreatmentNotes = treatmentNotes;
    }

    public void RecordVitals(string? bloodPressure, int? pulseBpm, decimal? temperatureCelsius, decimal? weightKg)
    {
        BloodPressure = bloodPressure;
        PulseBpm = pulseBpm;
        TemperatureCelsius = temperatureCelsius;
        WeightKg = weightKg;
    }
}
