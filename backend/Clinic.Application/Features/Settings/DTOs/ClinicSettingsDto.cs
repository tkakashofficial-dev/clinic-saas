namespace Clinic.Application.Features.Settings.DTOs;

/// <summary>The clinic's own profile — printed on prescriptions and intake
/// forms — plus its template preferences. Managed by the clinic Admin.</summary>
public class ClinicSettingsDto
{
    public string Name { get; set; } = default!;
    public string? Phone { get; set; }
    public string? Address { get; set; }
    /// <summary>"dental" | "general" — the pre-selected intake-form design.</summary>
    public string DefaultIntakeTemplate { get; set; } = "dental";

    /// <summary>Clinic's own UPI ID — enables "Collect via UPI" QR on invoices.</summary>
    public string? UpiId { get; set; }

    /// <summary>Public booking handle — the page lives at /book/{slug}.</summary>
    public string? Slug { get; set; }
    public bool PublicBookingEnabled { get; set; }
}

public class UpdateClinicSettingsRequest
{
    public string Name { get; set; } = default!;
    public string? Phone { get; set; }
    public string? Address { get; set; }
    public string DefaultIntakeTemplate { get; set; } = "dental";
    public string? UpiId { get; set; }
    public bool PublicBookingEnabled { get; set; }
}
