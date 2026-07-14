using Clinic.Application.Common.Models;
using Clinic.Application.Features.Staff.DTOs;
using Clinic.Application.Features.Staff.Services;
using Clinic.Domain.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Clinic.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class StaffController : ControllerBase
{
    private readonly IStaffService _staffService;

    public StaffController(IStaffService staffService)
    {
        _staffService = staffService;
    }

    [HttpPost]
    [Authorize(Roles = RoleNames.Admin)]
    public async Task<ActionResult<StaffDto>> AddStaff(
        [FromBody] AddStaffRequest request,
        CancellationToken cancellationToken)
        => Ok(await _staffService.AddStaffAsync(request, cancellationToken));

    /// <summary>Lost or expired invite? Send a fresh 7-day link.</summary>
    [HttpPost("{id}/resend-invite")]
    [Authorize(Roles = RoleNames.Admin)]
    public async Task<IActionResult> ResendInvite(Guid id, CancellationToken cancellationToken)
    {
        await _staffService.ResendInviteAsync(id, cancellationToken);
        return NoContent();
    }

    // All staff can READ the team list (reception needs doctors to book
    // appointments); only Admin can modify it
    [HttpGet]
    [Authorize(Roles = $"{RoleNames.Admin},{RoleNames.Doctor},{RoleNames.Receptionist}")]
    public async Task<ActionResult<PagedResult<StaffDto>>> GetAllStaff(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = PageRequest.DefaultPageSize,
        CancellationToken cancellationToken = default)
        => Ok(await _staffService.GetAllStaffAsync(
            new PageRequest(page, pageSize), cancellationToken));
}
