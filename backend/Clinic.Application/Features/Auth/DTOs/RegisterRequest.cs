namespace Clinic.Application.Features.Auth.DTOs;

public class RegisterRequest
{
    public string FirstName { get; set; } = default!;
    public string LastName { get; set; } = default!;
    public string Email { get; set; } = default!;
    public string Password { get; set; } = default!;
    public string ClinicName { get; set; } = default!;

    /// <summary>
    /// True when the owner is a practicing doctor (the common case in small
    /// clinics): they get the Doctor role in addition to Admin, so patients
    /// can be booked to them. Investor-owners leave this false and never
    /// appear in the doctor list.
    /// </summary>
    public bool OwnerIsDoctor { get; set; }
}
