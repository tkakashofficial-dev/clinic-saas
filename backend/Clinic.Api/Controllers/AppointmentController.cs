using Clinic.Application.Features.Appointments.DTOs;
using Clinic.Application.Features.Appointments.Services;
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
    [Authorize(Roles = "Admin,Receptionist")]
    public async Task<ActionResult<AppointmentDto>> CreateAppointment(
        [FromBody] CreateAppointmentRequest request,
        CancellationToken cancellationToken)
        => Ok(await _appointmentService.CreateAppointmentAsync(request, cancellationToken));

    [HttpGet]
    [Authorize(Roles = "Admin,Doctor,Receptionist")]
    public async Task<ActionResult<List<AppointmentDto>>> GetAppointments(
        [FromQuery] DateTime? date,
        [FromQuery] string? status,
        [FromQuery] Guid? doctorTenantUserId,
        CancellationToken cancellationToken)
        => Ok(await _appointmentService.GetAppointmentsAsync(
            date, status, doctorTenantUserId, cancellationToken));

    [HttpGet("{id}")]
    [Authorize(Roles = "Admin,Doctor,Receptionist")]
    public async Task<ActionResult<AppointmentDto>> GetAppointment(
        Guid id,
        CancellationToken cancellationToken)
        => Ok(await _appointmentService.GetAppointmentByIdAsync(id, cancellationToken));

    [HttpPatch("{id}/status")]
    [Authorize(Roles = "Admin,Doctor,Receptionist")]
    public async Task<ActionResult<AppointmentDto>> UpdateStatus(
        Guid id,
        [FromBody] UpdateAppointmentStatusRequest request,
        CancellationToken cancellationToken)
        => Ok(await _appointmentService.UpdateStatusAsync(id, request, cancellationToken));
}
