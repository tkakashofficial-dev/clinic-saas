using Clinic.Application.Common.Models;
using Clinic.Application.Features.Appointments.DTOs;
using Clinic.Application.Features.Appointments.Services;
using Clinic.Domain.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Clinic.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class AppointmentController : ControllerBase
{
    private readonly IAppointmentService _appointmentService;

    public AppointmentController(IAppointmentService appointmentService)
    {
        _appointmentService = appointmentService;
    }

    [HttpPost]
    [Authorize(Roles = $"{RoleNames.Admin},{RoleNames.Receptionist}")]
    public async Task<ActionResult<AppointmentDto>> CreateAppointment(
        [FromBody] CreateAppointmentRequest request,
        CancellationToken cancellationToken)
        => Ok(await _appointmentService.CreateAppointmentAsync(request, cancellationToken));

    [HttpGet]
    [Authorize(Roles = $"{RoleNames.Admin},{RoleNames.Doctor},{RoleNames.Receptionist}")]
    public async Task<ActionResult<PagedResult<AppointmentDto>>> GetAppointments(
        [FromQuery] DateTime? date,
        [FromQuery] string? status,
        [FromQuery] Guid? doctorTenantUserId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = PageRequest.DefaultPageSize,
        CancellationToken cancellationToken = default)
        => Ok(await _appointmentService.GetAppointmentsAsync(
            date, status, doctorTenantUserId, new PageRequest(page, pageSize), cancellationToken));

    [HttpGet("{id}")]
    [Authorize(Roles = $"{RoleNames.Admin},{RoleNames.Doctor},{RoleNames.Receptionist}")]
    public async Task<ActionResult<AppointmentDto>> GetAppointment(
        Guid id,
        CancellationToken cancellationToken)
        => Ok(await _appointmentService.GetAppointmentByIdAsync(id, cancellationToken));

    [HttpPatch("{id}/status")]
    [Authorize(Roles = $"{RoleNames.Admin},{RoleNames.Doctor},{RoleNames.Receptionist}")]
    public async Task<ActionResult<AppointmentDto>> UpdateStatus(
        Guid id,
        [FromBody] UpdateAppointmentStatusRequest request,
        CancellationToken cancellationToken)
        => Ok(await _appointmentService.UpdateStatusAsync(id, request, cancellationToken));
}
