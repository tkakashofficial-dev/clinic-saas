using Clinic.Domain.Common;

namespace Clinic.Domain.Entities;

/// <summary>
/// In-app notification for a staff member. Created by workflow events
/// (booking, check-in) and by the reminder engine. WhatsApp/SMS delivery
/// plugs in later as an additional channel for the same records.
/// </summary>
public class Notification : BaseEntity, IMustHaveTenant
{
    public Guid TenantId { get; private set; }
    public Guid RecipientTenantUserId { get; private set; }

    public string Type { get; private set; } = default!;   // NotificationTypes constants
    public string Title { get; private set; } = default!;
    public string Message { get; private set; } = default!;
    public bool IsRead { get; private set; }
    public DateTime? ReadAt { get; private set; }

    public TenantUser Recipient { get; private set; } = default!;

    private Notification() { }

    public Notification(
        Guid tenantId,
        Guid recipientTenantUserId,
        string type,
        string title,
        string message)
    {
        TenantId = tenantId;
        RecipientTenantUserId = recipientTenantUserId;
        Type = type;
        Title = title;
        Message = message;
    }

    public void MarkRead()
    {
        if (IsRead) return;
        IsRead = true;
        ReadAt = DateTime.UtcNow;
    }
}
