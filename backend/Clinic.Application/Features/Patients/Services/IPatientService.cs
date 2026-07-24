using Clinic.Application.Common.Models;
using Clinic.Application.Features.Patients.DTOs;

namespace Clinic.Application.Features.Patients.Services;

public interface IPatientService
{
    Task<PatientDto> RegisterPatientAsync(
        RegisterPatientRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>Corrects patient details — reception typos are a daily reality.</summary>
    Task<PatientDto> UpdatePatientAsync(
        Guid patientId,
        UpdatePatientRequest request,
        CancellationToken cancellationToken = default);

    Task<PagedResult<PatientDto>> GetAllPatientsAsync(
        string? search,
        PageRequest page,
        CancellationToken cancellationToken = default);

    Task<PatientDto> GetPatientByIdAsync(
        Guid patientId,
        CancellationToken cancellationToken = default);

    /// <summary>The patient's clinical story: every consultation, newest first.</summary>
    Task<PatientHistoryDto> GetHistoryAsync(
        Guid patientId,
        CancellationToken cancellationToken = default);

    /// <summary>Printable clinic-branded intake form, pre-filled with patient data.
    /// Template: "dental" or "general" — seeded designs, Admin picks per print.</summary>
    Task<(byte[] Content, string FileName)> GetIntakeFormPdfAsync(
        Guid patientId,
        string template = "dental",
        CancellationToken cancellationToken = default);

    /// <summary>The seeded condition list (allergies, diabetes…) that the
    /// register/edit forms render as tick-boxes.</summary>
    Task<List<MedicalConditionDto>> GetMedicalConditionsAsync(
        CancellationToken cancellationToken = default);

    /// <summary>Every patient as CSV — "your data is exportable" made true.</summary>
    Task<string> ExportCsvAsync(CancellationToken cancellationToken = default);

    /// <summary>Bulk import from CSV (paper/Excel migration). Forgiving on
    /// optional fields, strict on name+phone, skips duplicates by phone.</summary>
    Task<ImportResultDto> ImportCsvAsync(
        string csvText, CancellationToken cancellationToken = default);
}