namespace Clinic.Application.Features.Forms.DTOs;

/// <summary>A custom intake-form section, as the Forms page sees it.</summary>
public class IntakeFormSectionDto
{
    public Guid Id { get; set; }
    /// <summary>"dental" | "general" | "both".</summary>
    public string Template { get; set; } = default!;
    /// <summary>"box" | "lines" | "checklist".</summary>
    public string Kind { get; set; } = default!;
    public string Title { get; set; } = default!;
    public List<string> Items { get; set; } = new();
    public int SortOrder { get; set; }
}

public class SaveIntakeFormSectionRequest
{
    public string Template { get; set; } = "both";
    public string Kind { get; set; } = default!;
    public string Title { get; set; } = default!;
    public List<string> Items { get; set; } = new();
}
