namespace Clinic.Application.Features.Appointments.DTOs;

public class AppointmentDto
{
    public Guid Id { get; set; }
    public Guid PatientId { get; set; }
    public string PatientName { get; set; } = default!;
    public string PatientPhone { get; set; } = default!;
    public Guid DoctorTenantUserId { get; set; }
    public string DoctorName { get; set; } = default!;
    public DateTime AppointmentDate { get; set; }
    public string Status { get; set; } = default!;
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }
}