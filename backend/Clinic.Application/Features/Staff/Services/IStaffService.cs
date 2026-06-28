using Clinic.Application.Features.Staff.DTOs;

namespace Clinic.Application.Features.Staff.Services;

public interface IStaffService
{
    Task<StaffDto> AddStaffAsync(AddStaffRequest request, CancellationToken cancellationToken = default);
    Task<List<StaffDto>> GetAllStaffAsync(CancellationToken cancellationToken = default);
}