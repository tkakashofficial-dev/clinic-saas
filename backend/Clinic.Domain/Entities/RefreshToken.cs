using Clinic.Domain.Common;

namespace Clinic.Domain.Entities;

/// <summary>
/// Long-lived token used to obtain new access tokens without re-login.
/// Only a SHA-256 hash is stored — a database leak must not leak usable tokens.
/// Tokens are rotated on every use (the old one is revoked).
/// Belongs to the global SystemUser, not a tenant.
/// </summary>
public class RefreshToken : BaseEntity
{
    public Guid SystemUserId { get; private set; }
    public string TokenHash { get; private set; } = default!;
    public DateTime ExpiresAt { get; private set; }
    public DateTime? RevokedAt { get; private set; }

    public SystemUser SystemUser { get; private set; } = default!;

    public bool IsActive => RevokedAt is null && DateTime.UtcNow < ExpiresAt;

    private RefreshToken() { }

    public RefreshToken(Guid systemUserId, string tokenHash, DateTime expiresAt)
    {
        SystemUserId = systemUserId;
        TokenHash = tokenHash;
        ExpiresAt = expiresAt;
    }

    public void Revoke() => RevokedAt = DateTime.UtcNow;
}
