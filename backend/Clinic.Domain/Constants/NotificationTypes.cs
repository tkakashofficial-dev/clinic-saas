namespace Clinic.Domain.Constants;

public static class NotificationTypes
{
    public const string Booking = "Booking";
    public const string CheckIn = "CheckIn";
    public const string Reminder = "Reminder";
    /// <summary>Subscription events from the platform: payment received, plan changed.</summary>
    public const string Billing = "Billing";
    /// <summary>Stock crossed its reorder level — time to buy.</summary>
    public const string Inventory = "Inventory";
}
