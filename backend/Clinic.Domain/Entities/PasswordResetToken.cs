using Clinic.Domain.Common;

namespace Clinic.Domain.Entities;

/// <summary>
/// Single-use token for "forgot password" and staff invitations
/// ("set your password"). Only the SHA-256 hash is stored.
/// </summary>
public class PasswordResetToken : BaseEntity
{
    public Guid SystemUserId { get; private set; }
    public string TokenHash { get; private set; } = default!;
    public DateTime ExpiresAt { get; private set; }
    public DateTime? UsedAt { get; private set; }

    public SystemUser SystemUser { get; private set; } = default!;

    public bool IsValid => UsedAt is null && DateTime.UtcNow < ExpiresAt;

    private PasswordResetToken() { }

    public PasswordResetToken(Guid systemUserId, string tokenHash, DateTime expiresAt)
    {
        SystemUserId = systemUserId;
        TokenHash = tokenHash;
        ExpiresAt = expiresAt;
    }

    public void MarkUsed() => UsedAt = DateTime.UtcNow;
}
