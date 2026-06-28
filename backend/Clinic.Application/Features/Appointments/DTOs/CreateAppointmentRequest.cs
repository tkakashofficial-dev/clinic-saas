namespace Clinic.Application.Features.Appointments.DTOs;

public class CreateAppointmentRequest
{
    public Guid PatientId { get; set; }
    public Guid DoctorTenantUserId { get; set; }
    public DateTime AppointmentDate { get; set; }
    public string? Notes { get; set; }
}