using Clinic.Application.Features.Staff.DTOs;
using Clinic.Application.Features.Staff.Services;
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
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<StaffDto>> AddStaff(
        [FromBody] AddStaffRequest request,
        CancellationToken cancellationToken)
        => Ok(await _staffService.AddStaffAsync(request, cancellationToken));

    [HttpGet]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<List<StaffDto>>> GetAllStaff(
        CancellationToken cancellationToken)
        => Ok(await _staffService.GetAllStaffAsync(cancellationToken));
}
