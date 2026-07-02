namespace Clinic.Domain.Constants;

/// <summary>
/// The closed set of role names. [Authorize(Roles = ...)] matches by EXACT string,
/// so roles must never come from free-text input — always from these constants.
/// </summary>
public static class RoleNames
{
    public const string Admin = "Admin";
    public const string Doctor = "Doctor";
    public const string Receptionist = "Receptionist";

    public static readonly IReadOnlyList<string> All = [Admin, Doctor, Receptionist];

    public static string DescriptionOf(string role) => role switch
    {
        Admin => "Clinic administrator with full access",
        Doctor => "Medical staff: views patients, manages appointments",
        Receptionist => "Front desk: registers patients, schedules appointments",
        _ => string.Empty
    };

    /// <summary>Case-insensitive match against the closed set; returns the canonical casing.</summary>
    public static bool TryNormalize(string? role, out string normalized)
    {
        normalized = All.FirstOrDefault(r =>
            string.Equals(r, role, StringComparison.OrdinalIgnoreCase)) ?? string.Empty;
        return normalized.Length > 0;
    }
}
