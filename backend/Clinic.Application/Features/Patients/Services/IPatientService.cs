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
}