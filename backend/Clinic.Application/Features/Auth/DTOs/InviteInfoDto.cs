namespace Clinic.Application.Features.Auth.DTOs;

/// <summary>Context for the accept-invite page: who is joining what.</summary>
public class InviteInfoDto
{
    public string FirstName { get; set; } = default!;
    public string Email { get; set; } = default!;
    public List<string> ClinicNames { get; set; } = new();
}
