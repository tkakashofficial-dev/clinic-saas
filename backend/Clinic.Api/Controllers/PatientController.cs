using Clinic.Application.Common.Models;
using Clinic.Application.Features.Patients.DTOs;
using Clinic.Application.Features.Patients.Services;
using Clinic.Domain.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Clinic.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class PatientController : ControllerBase
{
    private readonly IPatientService _patientService;

    public PatientController(IPatientService patientService)
    {
        _patientService = patientService;
    }

    [HttpPost]
    [Authorize(Roles = $"{RoleNames.Admin},{RoleNames.Receptionist}")]
    public async Task<ActionResult<PatientDto>> RegisterPatient(
        [FromBody] RegisterPatientRequest request,
        CancellationToken cancellationToken)
        => Ok(await _patientService.RegisterPatientAsync(request, cancellationToken));

    [HttpPut("{id}")]
    [Authorize(Roles = $"{RoleNames.Admin},{RoleNames.Receptionist}")]
    public async Task<ActionResult<PatientDto>> UpdatePatient(
        Guid id,
        [FromBody] UpdatePatientRequest request,
        CancellationToken cancellationToken)
        => Ok(await _patientService.UpdatePatientAsync(id, request, cancellationToken));

    [HttpGet]
    [Authorize(Roles = $"{RoleNames.Admin},{RoleNames.Doctor},{RoleNames.Receptionist}")]
    public async Task<ActionResult<PagedResult<PatientDto>>> GetAllPatients(
        [FromQuery] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = PageRequest.DefaultPageSize,
        CancellationToken cancellationToken = default)
        => Ok(await _patientService.GetAllPatientsAsync(
            search, new PageRequest(page, pageSize), cancellationToken));

    [HttpGet("{id}")]
    [Authorize(Roles = $"{RoleNames.Admin},{RoleNames.Doctor},{RoleNames.Receptionist}")]
    public async Task<ActionResult<PatientDto>> GetPatient(
        Guid id,
        CancellationToken cancellationToken)
        => Ok(await _patientService.GetPatientByIdAsync(id, cancellationToken));

    /// <summary>The patient's clinical story — every consultation, newest first.</summary>
    [HttpGet("{id}/history")]
    [Authorize(Roles = $"{RoleNames.Admin},{RoleNames.Doctor},{RoleNames.Receptionist}")]
    public async Task<ActionResult<PatientHistoryDto>> GetHistory(
        Guid id,
        CancellationToken cancellationToken)
        => Ok(await _patientService.GetHistoryAsync(id, cancellationToken));

    /// <summary>Printable clinic-branded intake form, pre-filled with patient data.</summary>
    [HttpGet("{id}/intake-form")]
    [Authorize(Roles = $"{RoleNames.Admin},{RoleNames.Doctor},{RoleNames.Receptionist}")]
    public async Task<IActionResult> GetIntakeForm(Guid id, CancellationToken cancellationToken)
    {
        var (content, fileName) = await _patientService.GetIntakeFormPdfAsync(id, cancellationToken);
        return File(content, "application/pdf", fileName);
    }
}
