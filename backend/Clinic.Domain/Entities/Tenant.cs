using Clinic.Domain.Common;
using Clinic.Domain.Enums;

namespace Clinic.Domain.Entities;

public class Tenant : BaseEntity
{
    public string Name { get; private set; } = default!;
    public string? Phone { get; private set; }
    public string? Address { get; private set; }
    public bool IsActive { get; private set; } = true;

    // Subscription: every new clinic starts on a full-featured Clinic trial,
    // then picks (or is downgraded to) a paid tier
    public PlanType Plan { get; private set; } = PlanType.Clinic;
    public DateTime? TrialEndsAt { get; private set; }

    public bool IsInTrial => TrialEndsAt.HasValue && DateTime.UtcNow < TrialEndsAt.Value;

    /// <summary>True when the trial lapsed and no plan was ever chosen.</summary>
    public bool TrialExpired => TrialEndsAt.HasValue && DateTime.UtcNow >= TrialEndsAt.Value;

    /// <summary>
    /// What the tenant is actually entitled to RIGHT NOW:
    /// in trial -> full Clinic tier; trial lapsed without choosing -> Solo floor;
    /// otherwise the chosen plan. All enforcement must use THIS, never Plan.
    /// </summary>
    public PlanType EffectivePlan =>
        IsInTrial ? PlanType.Clinic
        : TrialExpired ? PlanType.Solo
        : Plan;

    private Tenant() { }

    public Tenant(string name, string? phone = null, string? address = null)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        Phone = phone;
        Address = address;
        TrialEndsAt = DateTime.UtcNow.AddDays(14);
    }

    public void ChangePlan(PlanType plan)
    {
        Plan = plan;
        TrialEndsAt = null;   // choosing a plan ends the trial
    }

    public void Update(string name, string? phone, string? address)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        Phone = phone;
        Address = address;
    }

    public void Deactivate() => IsActive = false;
    public void Activate() => IsActive = true;
}