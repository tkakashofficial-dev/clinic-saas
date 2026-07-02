using Clinic.Domain.Common;
using Clinic.Domain.Enums;

namespace Clinic.Domain.Entities;

public class Appointment : BaseEntity, IMustHaveTenant
{
    public Guid TenantId { get; private set; }
    public Guid PatientId { get; private set; }
    public Guid DoctorTenantUserId { get; private set; }
    public Guid CreatedByTenantUserId { get; private set; }
    public DateTime AppointmentDate { get; private set; }
    public AppointmentStatus Status { get; private set; } = AppointmentStatus.Scheduled;
    public string? Notes { get; private set; }
    /// <summary>Set once the reminder engine has notified about this appointment.</summary>
    public DateTime? ReminderSentAt { get; private set; }

    public Tenant Tenant { get; private set; } = default!;
    public Patient Patient { get; private set; } = default!;
    public TenantUser Doctor { get; private set; } = default!;
    // Named CreatedByUser (not CreatedBy) to avoid hiding BaseEntity.CreatedBy audit field
    public TenantUser CreatedByUser { get; private set; } = default!;

    private Appointment() { }

    public Appointment(
        Guid tenantId,
        Guid patientId,
        Guid doctorTenantUserId,
        Guid createdByTenantUserId,
        DateTime appointmentDate,
        string? notes = null)
    {
        TenantId = tenantId;
        PatientId = patientId;
        DoctorTenantUserId = doctorTenantUserId;
        CreatedByTenantUserId = createdByTenantUserId;
        AppointmentDate = appointmentDate;
        Notes = notes;
    }

    public void UpdateStatus(AppointmentStatus status) => Status = status;
    public void UpdateNotes(string? notes) => Notes = notes;
    public void MarkReminderSent() => ReminderSentAt = DateTime.UtcNow;
}