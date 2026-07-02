using Clinic.Application.Common.Models;
using Clinic.Application.Features.Appointments.DTOs;
using Clinic.Application.Features.Appointments.Services;
using Clinic.Application.Features.Consultations.DTOs;
using Clinic.Application.Features.Consultations.Services;
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
    private readonly IConsultationService _consultationService;

    public AppointmentController(
        IAppointmentService appointmentService,
        IConsultationService consultationService)
    {
        _appointmentService = appointmentService;
        _consultationService = consultationService;
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

    // Consultation is a sub-resource of the appointment: the clinical outcome

    [HttpPost("{id}/consultation")]
    [Authorize(Roles = $"{RoleNames.Admin},{RoleNames.Doctor}")]
    public async Task<ActionResult<ConsultationDto>> RecordConsultation(
        Guid id,
        [FromBody] RecordConsultationRequest request,
        CancellationToken cancellationToken)
        => Ok(await _consultationService.RecordAsync(id, request, cancellationToken));

    [HttpGet("{id}/consultation")]
    [Authorize(Roles = $"{RoleNames.Admin},{RoleNames.Doctor},{RoleNames.Receptionist}")]
    public async Task<ActionResult<ConsultationDto>> GetConsultation(
        Guid id,
        CancellationToken cancellationToken)
        => Ok(await _consultationService.GetByAppointmentAsync(id, cancellationToken));
}
