namespace Clinic.Domain.Constants;

/// <summary>The eight ABO/Rh blood groups — the only valid values for
/// Patient.BloodGroup. Kept in Domain so validators and any future
/// emergency-card printing share one source of truth.</summary>
public static class BloodGroups
{
    public static readonly string[] All = ["A+", "A-", "B+", "B-", "AB+", "AB-", "O+", "O-"];

    public static bool IsValid(string value) => All.Contains(value);
}
