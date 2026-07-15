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

/// <summary>Answers captured when staff fill the intake form digitally by
/// asking the patient. Prints PRE-FILLED on the intake PDF.</summary>
public class IntakeAnswersDto
{
    /// <summary>Checked items from the disease checklist (exact labels).</summary>
    public List<string> DiseaseChecklist { get; set; } = new();
    public string? ChiefComplaint { get; set; }
    public string? MedicalHistory { get; set; }
    /// <summary>Dental history (dental) / surgical & family history (general).</summary>
    public string? SecondaryHistory { get; set; }
    public string? Medications { get; set; }
    public List<CustomSectionAnswerDto> Custom { get; set; } = new();
}

public class CustomSectionAnswerDto
{
    public Guid SectionId { get; set; }
    /// <summary>For "box" sections — free text.</summary>
    public string? Text { get; set; }
    /// <summary>For "checklist" sections — the ticked labels.</summary>
    public List<string> Checked { get; set; } = new();
    /// <summary>For "lines" sections — label → value.</summary>
    public Dictionary<string, string> Lines { get; set; } = new();
}

public class IntakeFormResponseDto
{
    public string Template { get; set; } = default!;
    public IntakeAnswersDto Answers { get; set; } = new();
    public DateTime FilledAt { get; set; }
    public string FilledByName { get; set; } = default!;
}

public class SaveIntakeFormResponseRequest
{
    public string Template { get; set; } = "dental";
    public IntakeAnswersDto Answers { get; set; } = new();
}
