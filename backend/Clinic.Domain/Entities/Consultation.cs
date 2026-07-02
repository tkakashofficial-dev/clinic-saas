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
}
