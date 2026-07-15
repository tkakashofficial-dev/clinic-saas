using Clinic.Application.Features.Forms.DTOs;

namespace Clinic.Application.Features.Forms.Services;

/// <summary>
/// The clinic's form builder (v1): Admin manages custom sections that print
/// on the intake form; everyone can preview the result with sample data.
/// </summary>
public interface IFormsService
{
    Task<List<IntakeFormSectionDto>> GetSectionsAsync(CancellationToken cancellationToken = default);

    Task<IntakeFormSectionDto> CreateSectionAsync(
        SaveIntakeFormSectionRequest request, CancellationToken cancellationToken = default);

    Task<IntakeFormSectionDto> UpdateSectionAsync(
        Guid sectionId, SaveIntakeFormSectionRequest request, CancellationToken cancellationToken = default);

    Task DeleteSectionAsync(Guid sectionId, CancellationToken cancellationToken = default);

    /// <summary>Swap with the neighbour above (-1) or below (+1).</summary>
    Task<List<IntakeFormSectionDto>> MoveSectionAsync(
        Guid sectionId, int direction, CancellationToken cancellationToken = default);

    /// <summary>The intake form PDF rendered with SAMPLE patient data —
    /// so admins see exactly what reception will print.</summary>
    Task<(byte[] Content, string FileName)> PreviewPdfAsync(
        string template, CancellationToken cancellationToken = default);

    /// <summary>Staff filled the form digitally by asking the patient —
    /// saves the answers to the patient's record.</summary>
    Task<IntakeFormResponseDto> SaveResponseAsync(
        Guid patientId, SaveIntakeFormResponseRequest request, CancellationToken cancellationToken = default);

    /// <summary>The patient's latest digital answers (null = never filled).</summary>
    Task<IntakeFormResponseDto?> GetLatestResponseAsync(
        Guid patientId, CancellationToken cancellationToken = default);
}
