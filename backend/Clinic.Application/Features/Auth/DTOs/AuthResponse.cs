namespace Clinic.Application.Features.Auth.DTOs;

public class AuthResponse
{
    public string AccessToken { get; set; } = default!;
    public string Email { get; set; } = default!;
    public string FullName { get; set; } = default!;
    public string Role { get; set; } = default!;
    public Guid TenantId { get; set; }
    public Guid TenantUserId { get; set; }
    public DateTime ExpiresAt { get; set; }
}