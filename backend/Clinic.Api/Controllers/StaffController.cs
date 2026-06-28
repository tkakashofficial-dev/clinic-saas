using Clinic.Application.Features.Staff.DTOs;
using Clinic.Application.Features.Staff.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Clinic.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize] // every endpoint here requires JWT token
public class StaffController : ControllerBase
{
    private readonly IStaffService _staffService;

    public StaffController(IStaffService staffService)
    {
        _staffService = staffService;
    }

    [HttpPost]
    [Authorize(Roles = "Admin")] // only Admin can add staff
    public async Task<IActionResult> AddStaff(
        [FromBody] AddStaffRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await _staffService.AddStaffAsync(request, cancellationToken);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }

    [HttpGet]
    [Authorize(Roles = "Admin")] // only Admin can list staff
    public async Task<IActionResult> GetAllStaff(CancellationToken cancellationToken)
    {
        var result = await _staffService.GetAllStaffAsync(cancellationToken);
        return Ok(result);
    }
}