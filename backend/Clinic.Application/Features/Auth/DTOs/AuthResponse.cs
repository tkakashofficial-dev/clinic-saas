namespace Clinic.Application.Features.Auth.DTOs;

public class AuthResponse
{
    public string AccessToken { get; set; } = default!;
    public string RefreshToken { get; set; } = default!;
    public string Email { get; set; } = default!;
    public string FullName { get; set; } = default!;
    /// <summary>Primary role (highest privilege) — for display.</summary>
    public string Role { get; set; } = default!;
    /// <summary>All roles this user holds in the clinic.</summary>
    public List<string> Roles { get; set; } = new();
    public Guid TenantId { get; set; }
    public Guid TenantUserId { get; set; }
    public DateTime ExpiresAt { get; set; }
}