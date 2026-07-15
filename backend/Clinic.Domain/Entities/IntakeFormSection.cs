using Clinic.Domain.Common;

namespace Clinic.Domain.Entities;

/// <summary>
/// A custom section the clinic Admin adds to their intake form — the v1 of
/// the form builder. Three shapes cover what real paper forms contain:
///   box       — a titled area with writing space
///   lines     — a title + labelled dotted lines (e.g. "Allergy details")
///   checklist — a title + tick-box items
/// Sections print on an extra page of the intake PDF, in SortOrder.
/// </summary>
public class IntakeFormSection : BaseEntity, IMustHaveTenant
{
    public Guid TenantId { get; private set; }

    /// <summary>Which template it belongs to: "dental", "general" or "both".</summary>
    public string Template { get; private set; } = default!;
    /// <summary>"box" | "lines" | "checklist".</summary>
    public string Kind { get; private set; } = default!;
    public string Title { get; private set; } = default!;
    /// <summary>JSON string[] — the checklist items or line labels ([] for box).</summary>
    public string ItemsJson { get; private set; } = "[]";
    public int SortOrder { get; private set; }

    public Tenant Tenant { get; private set; } = default!;

    private IntakeFormSection() { }

    public IntakeFormSection(
        Guid tenantId, string template, string kind, string title,
        string itemsJson, int sortOrder)
    {
        TenantId = tenantId;
        Template = template ?? throw new ArgumentNullException(nameof(template));
        Kind = kind ?? throw new ArgumentNullException(nameof(kind));
        Title = title ?? throw new ArgumentNullException(nameof(title));
        ItemsJson = itemsJson ?? "[]";
        SortOrder = sortOrder;
    }

    public void Update(string template, string kind, string title, string itemsJson)
    {
        Template = template ?? throw new ArgumentNullException(nameof(template));
        Kind = kind ?? throw new ArgumentNullException(nameof(kind));
        Title = title ?? throw new ArgumentNullException(nameof(title));
        ItemsJson = itemsJson ?? "[]";
    }

    public void MoveTo(int sortOrder) => SortOrder = sortOrder;
}
