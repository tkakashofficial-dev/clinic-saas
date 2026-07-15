using Clinic.Domain.Enums;

namespace Clinic.Domain.Constants;

/// <summary>
/// The single source of truth for what each plan allows.
/// Enforcement reads THIS — never scatter limit numbers through services.
/// </summary>
public static class PlanLimits
{
    public static int MaxStaff(PlanType plan) => plan switch
    {
        PlanType.Solo => 2,        // the doctor + 1 receptionist
        PlanType.Clinic => 10,
        PlanType.Growth => int.MaxValue,
        _ => 2
    };

    public static int MaxDoctors(PlanType plan) => plan switch
    {
        PlanType.Solo => 1,
        PlanType.Clinic => 5,
        PlanType.Growth => int.MaxValue,
        _ => 1
    };

    /// <summary>Pharmacy & inventory is the Clinic tier's headline feature —
    /// the upgrade lever for Solo doctors whose practice is growing.</summary>
    public static bool HasInventory(PlanType plan) => plan != PlanType.Solo;

    public static string DisplayName(PlanType plan) => plan switch
    {
        PlanType.Solo => "Solo",
        PlanType.Clinic => "Clinic",
        PlanType.Growth => "Growth",
        _ => plan.ToString()
    };
}
