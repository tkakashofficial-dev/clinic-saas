using Clinic.Domain.Common;

namespace Clinic.Domain.Entities;

/// <summary>
/// A digitally-filled intake form: reception or the doctor asks the patient
/// and ticks/types the answers instead of handing over paper. The answers
/// live on the patient's record and print PRE-FILLED on the intake PDF.
/// Latest response wins; older ones remain as history.
/// </summary>
public class IntakeFormResponse : BaseEntity, IMustHaveTenant
{
    public Guid TenantId { get; private set; }
    public Guid PatientId { get; private set; }
    /// <summary>Which template was answered: "dental" | "general".</summary>
    public string Template { get; private set; } = default!;
    /// <summary>Serialized IntakeAnswersDto — the frontend owns the shape.</summary>
    public string AnswersJson { get; private set; } = default!;
    public Guid FilledByTenantUserId { get; private set; }

    public Patient Patient { get; private set; } = default!;
    public TenantUser FilledBy { get; private set; } = default!;

    private IntakeFormResponse() { }

    public IntakeFormResponse(
        Guid tenantId, Guid patientId, string template,
        string answersJson, Guid filledByTenantUserId)
    {
        TenantId = tenantId;
        PatientId = patientId;
        Template = template ?? throw new ArgumentNullException(nameof(template));
        AnswersJson = answersJson ?? throw new ArgumentNullException(nameof(answersJson));
        FilledByTenantUserId = filledByTenantUserId;
    }
}
