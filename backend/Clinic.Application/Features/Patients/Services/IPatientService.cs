using Clinic.Application.Features.Patients.DTOs;

namespace Clinic.Application.Features.Patients.Services;

public interface IPatientService
{
    Task<PatientDto> RegisterPatientAsync(
        RegisterPatientRequest request,
        CancellationToken cancellationToken = default);

    Task<List<PatientDto>> GetAllPatientsAsync(
        string? search,
        CancellationToken cancellationToken = default);

    Task<PatientDto> GetPatientByIdAsync(
        Guid patientId,
        CancellationToken cancellationToken = default);
}