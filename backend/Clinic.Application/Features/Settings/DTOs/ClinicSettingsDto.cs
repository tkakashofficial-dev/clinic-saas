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
}

public class UpdateClinicSettingsRequest
{
    public string Name { get; set; } = default!;
    public string? Phone { get; set; }
    public string? Address { get; set; }
    public string DefaultIntakeTemplate { get; set; } = "dental";
}
