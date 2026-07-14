namespace Clinic.Application.Features.Staff.DTOs;

public class AddStaffRequest
{
    public string FirstName { get; set; } = default!;
    public string LastName { get; set; } = default!;
    public string Email { get; set; } = default!;

    /// <summary>
    /// Optional. Empty = invite-only: the account gets an unguessable random
    /// password and the staff member sets their own via the emailed link.
    /// </summary>
    public string? Password { get; set; }

    /// <summary>
    /// One or more roles. Roles combine: a partner who also practices gets
    /// ["Admin", "Doctor"]; a hired dentist gets ["Doctor"].
    /// </summary>
    public List<string> Roles { get; set; } = new();
}
