namespace Clinic.Application.Features.PublicBooking.DTOs;

/// <summary>What an anonymous visitor sees on /book/{slug} — public info
/// only: the clinic's letterhead basics and who they can book with.</summary>
public class PublicClinicDto
{
    public string Name { get; set; } = default!;
    public string? Address { get; set; }
    public string? Phone { get; set; }
    public List<PublicDoctorDto> Doctors { get; set; } = new();
}

public class PublicDoctorDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = default!;
}

public class PublicBookingRequest
{
    public Guid DoctorId { get; set; }
    /// <summary>UTC instant chosen by the patient (frontend converts).</summary>
    public DateTime AppointmentAt { get; set; }
    public string PatientName { get; set; } = default!;
    public string Phone { get; set; } = default!;
    public string? Note { get; set; }
    /// <summary>Honeypot — humans never see this field; bots fill it.</summary>
    public string? Website { get; set; }
}

public class PublicBookingResultDto
{
    public string ClinicName { get; set; } = default!;
    public string DoctorName { get; set; } = default!;
    public DateTime AppointmentAt { get; set; }
}
