using Clinic.Application.Features.Consultations.DTOs;

namespace Clinic.Application.Features.Consultations.Services;

public interface IConsultationService
{
    /// <summary>Records the clinical outcome of an appointment and marks it Completed.</summary>
    Task<ConsultationDto> RecordAsync(
        Guid appointmentId,
        RecordConsultationRequest request,
        CancellationToken cancellationToken = default);

    Task<ConsultationDto> GetByAppointmentAsync(
        Guid appointmentId,
        CancellationToken cancellationToken = default);

    /// <summary>Renders the prescription as a downloadable PDF.</summary>
    Task<(byte[] Content, string FileName)> GetPrescriptionPdfAsync(
        Guid prescriptionId,
        CancellationToken cancellationToken = default);
}
