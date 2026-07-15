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
    public DateTime CreatedAt { get; set; }
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
