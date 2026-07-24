using Clinic.Domain.Common;
using Clinic.Domain.Enums;

namespace Clinic.Domain.Entities;

public class Tenant : BaseEntity
{
    public string Name { get; private set; } = default!;
    public string? Phone { get; private set; }
    public string? Address { get; private set; }
    public bool IsActive { get; private set; } = true;

    /// <summary>Which seeded intake-form design this clinic prints by default
    /// ("dental" | "general") — the Admin picks it in Settings.</summary>
    public string DefaultIntakeTemplate { get; private set; } = "dental";

    /// <summary>The clinic's own UPI ID (name@bank). Printed as a QR on unpaid
    /// invoices so patients pay the CLINIC directly — Klivia never touches
    /// patient money. Null until the Admin sets it in Settings.</summary>
    public string? UpiId { get; private set; }

    /// <summary>URL handle for the public booking page (/book/{slug}).
    /// Null until online booking is first enabled.</summary>
    public string? Slug { get; private set; }

    /// <summary>Patients can self-book at /book/{slug} when true.</summary>
    public bool PublicBookingEnabled { get; private set; }

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

    public void SetDefaultIntakeTemplate(string template) =>
        DefaultIntakeTemplate = template ?? throw new ArgumentNullException(nameof(template));

    public void SetUpiId(string? upiId) => UpiId = upiId;

    /// <summary>Slug is write-once: printed QR codes and shared links must
    /// never break, so once minted it sticks even if the clinic renames.</summary>
    public void AssignSlug(string slug)
    {
        if (Slug is not null) return;
        Slug = slug ?? throw new ArgumentNullException(nameof(slug));
    }

    public void SetPublicBooking(bool enabled) => PublicBookingEnabled = enabled;

    public void Deactivate() => IsActive = false;
    public void Activate() => IsActive = true;
}