namespace Clinic.Application.Features.Auth.DTOs;

public class RefreshRequest
{
    public string RefreshToken { get; set; } = default!;

    /// <summary>Which clinic the session was scoped to when the token
    /// expired. Without this, a silent mid-work refresh dumped multi-clinic
    /// users back into their FIRST clinic — while the screen still showed
    /// the second one. Safe to trust: IssueForUserAsync only ever issues
    /// tenants the user is an active member of.</summary>
    public Guid? TenantId { get; set; }
}
