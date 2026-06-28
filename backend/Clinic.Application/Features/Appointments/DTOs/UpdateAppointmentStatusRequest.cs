namespace Clinic.Application.Features.Appointments.DTOs;

public class UpdateAppointmentStatusRequest
{
    public string Status { get; set; } = default!;
    // Scheduled, InProgress, Completed, Cancelled
}