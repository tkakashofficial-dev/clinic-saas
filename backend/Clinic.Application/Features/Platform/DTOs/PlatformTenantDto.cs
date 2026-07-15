namespace Clinic.Application.Features.Platform.DTOs;

/// <summary>One row in the platform owner's tenant console.</summary>
public class PlatformTenantDto
{
    public Guid TenantId { get; set; }
    public string Name { get; set; } = default!;
    public string Plan { get; set; } = default!;
    public bool IsInTrial { get; set; }
    public DateTime? TrialEndsAt { get; set; }
    public bool IsActive { get; set; }
    public int StaffCount { get; set; }
    public int PatientCount { get; set; }
    /// <summary>Who to call/WhatsApp when payment is due — the first Admin.</summary>
    public string? OwnerName { get; set; }
    public string? OwnerEmail { get; set; }
    public string? ClinicPhone { get; set; }
    public string? ClinicAddress { get; set; }
    /// <summary>Subscription coverage end from recorded payments (null = never paid).</summary>
    public DateTime? PaidUntil { get; set; }
    public DateTime? LastPaymentAt { get; set; }
    public decimal? LastPaymentAmount { get; set; }
    /// <summary>Coverage lapsed (or never paid after trial) — time to call them.</summary>
    public bool PaymentOverdue { get; set; }
    public DateTime CreatedAt { get; set; }
}

/// <summary>Manual payment collection: UPI/bank/cash today, gateway later.</summary>
public class RecordPaymentRequest
{
    public decimal AmountRupees { get; set; }
    /// <summary>PaymentMethods constants: Upi, BankTransfer, Cash, Other.</summary>
    public string Method { get; set; } = default!;
    /// <summary>Months of subscription this payment covers (extends remaining time).</summary>
    public int PeriodMonths { get; set; } = 1;
    /// <summary>When the money actually arrived (default: now). The owner may
    /// record on Monday a UPI that landed on Saturday.</summary>
    public DateTime? PaidAt { get; set; }
    public string? Note { get; set; }
    /// <summary>Optionally apply a plan in the same step (payment → activation).</summary>
    public string? PlanToApply { get; set; }
}

/// <summary>One row of a clinic's payment history in the console.</summary>
public class PlatformPaymentDto
{
    public DateTime PaidAt { get; set; }
    public decimal AmountRupees { get; set; }
    public string Method { get; set; } = default!;
    public int PeriodMonths { get; set; }
    public DateTime PaidUntil { get; set; }
    public string? Note { get; set; }
    public string RecordedByEmail { get; set; } = default!;
}

public class PlatformChangePlanRequest
{
    public string Plan { get; set; } = default!;
}

public class PlatformSetActiveRequest
{
    public bool IsActive { get; set; }
}

/// <summary>Outcome of the production email self-test.</summary>
public class PlatformEmailTestResult
{
    public bool Sent { get; set; }
    public string To { get; set; } = default!;
    public string Detail { get; set; } = default!;
}
