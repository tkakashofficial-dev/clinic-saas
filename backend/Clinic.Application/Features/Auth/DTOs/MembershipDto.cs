namespace Clinic.Application.Features.Auth.DTOs;

/// <summary>One clinic a user belongs to — powers the clinic switcher.</summary>
public class MembershipDto
{
    public Guid TenantId { get; set; }
    public string ClinicName { get; set; } = default!;
}
