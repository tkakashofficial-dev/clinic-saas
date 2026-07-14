using Clinic.Application.Common.Models;
using Clinic.Application.Features.Staff.DTOs;

namespace Clinic.Application.Features.Staff.Services;

public interface IStaffService
{
    Task<StaffDto> AddStaffAsync(AddStaffRequest request, CancellationToken cancellationToken = default);
    Task<PagedResult<StaffDto>> GetAllStaffAsync(PageRequest page, CancellationToken cancellationToken = default);

    /// <summary>Re-sends the set-your-password invite (new 7-day link) — lost emails and expired links happen.</summary>
    Task ResendInviteAsync(Guid tenantUserId, CancellationToken cancellationToken = default);
}