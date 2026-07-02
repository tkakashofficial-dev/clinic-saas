using Clinic.Application.Common.Models;
using Clinic.Application.Features.Appointments.DTOs;

namespace Clinic.Application.Features.Appointments.Services;

public interface IAppointmentService
{
    Task<AppointmentDto> CreateAppointmentAsync(
        CreateAppointmentRequest request,
        CancellationToken cancellationToken = default);

    Task<PagedResult<AppointmentDto>> GetAppointmentsAsync(
        DateTime? date,
        string? status,
        Guid? doctorTenantUserId,
        PageRequest page,
        CancellationToken cancellationToken = default);

    Task<AppointmentDto> GetAppointmentByIdAsync(
        Guid appointmentId,
        CancellationToken cancellationToken = default);

    Task<AppointmentDto> UpdateStatusAsync(
        Guid appointmentId,
        UpdateAppointmentStatusRequest request,
        CancellationToken cancellationToken = default);
}