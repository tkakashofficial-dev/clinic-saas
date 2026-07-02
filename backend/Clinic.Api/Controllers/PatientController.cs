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
}
