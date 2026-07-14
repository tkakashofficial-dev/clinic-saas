namespace Clinic.Application.Features.Staff.DTOs;

public class StaffDto
{
    public Guid Id { get; set; }           // TenantUserId
    public Guid SystemUserId { get; set; }
    public string FullName { get; set; } = default!;
    public string Email { get; set; } = default!;
    public List<string> Roles { get; set; } = new();
    public bool IsActive { get; set; }
    /// <summary>True when the person already had a Klivia account (e.g. works at
    /// another clinic) and this clinic was attached to it.</summary>
    public bool ExistingAccount { get; set; }
    public DateTime CreatedAt { get; set; }
}
