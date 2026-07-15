namespace Clinic.Domain.Constants;

/// <summary>Invoice lifecycle — a closed set so filters stay honest.</summary>
public static class InvoiceStatuses
{
    public const string Unpaid = "Unpaid";
    public const string Paid = "Paid";
    public const string Cancelled = "Cancelled";
}
