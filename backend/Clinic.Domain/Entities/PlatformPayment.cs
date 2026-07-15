using Clinic.Domain.Common;

namespace Clinic.Domain.Entities;

/// <summary>
/// A subscription payment collected from a clinic (UPI/bank/cash today,
/// gateway later). PLATFORM-level data: deliberately NOT IMustHaveTenant —
/// clinics never see these rows; only the platform console reads them.
/// </summary>
public class PlatformPayment : BaseEntity
{
    public Guid TenantId { get; private set; }
    public decimal AmountRupees { get; private set; }
    /// <summary>PaymentMethods constants: Upi, BankTransfer, Cash, Other.</summary>
    public string Method { get; private set; } = default!;
    public DateTime PaidAt { get; private set; }
    /// <summary>How many months of subscription this payment covers.</summary>
    public int PeriodMonths { get; private set; }
    /// <summary>Coverage end AFTER this payment (extends any remaining time).</summary>
    public DateTime PaidUntil { get; private set; }
    public string? Note { get; private set; }
    /// <summary>Which platform admin recorded it — the audit trail.</summary>
    public string RecordedByEmail { get; private set; } = default!;

    public Tenant Tenant { get; private set; } = default!;

    private PlatformPayment() { }

    public PlatformPayment(
        Guid tenantId,
        decimal amountRupees,
        string method,
        int periodMonths,
        DateTime paidUntil,
        string recordedByEmail,
        string? note = null)
    {
        if (amountRupees <= 0) throw new ArgumentOutOfRangeException(nameof(amountRupees));
        if (periodMonths <= 0) throw new ArgumentOutOfRangeException(nameof(periodMonths));

        TenantId = tenantId;
        AmountRupees = amountRupees;
        Method = method ?? throw new ArgumentNullException(nameof(method));
        PaidAt = DateTime.UtcNow;
        PeriodMonths = periodMonths;
        PaidUntil = paidUntil;
        RecordedByEmail = recordedByEmail ?? throw new ArgumentNullException(nameof(recordedByEmail));
        Note = note;
    }
}
