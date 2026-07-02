using Clinic.Application.Features.Patients.DTOs;
using Clinic.Application.Features.Patients.Services;
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
    [Authorize(Roles = "Admin,Receptionist")]
    public async Task<ActionResult<PatientDto>> RegisterPatient(
        [FromBody] RegisterPatientRequest request,
        CancellationToken cancellationToken)
        => Ok(await _patientService.RegisterPatientAsync(request, cancellationToken));

    [HttpGet]
    [Authorize(Roles = "Admin,Doctor,Receptionist")]
    public async Task<ActionResult<List<PatientDto>>> GetAllPatients(
        [FromQuery] string? search,
        CancellationToken cancellationToken)
        => Ok(await _patientService.GetAllPatientsAsync(search, cancellationToken));

    [HttpGet("{id}")]
    [Authorize(Roles = "Admin,Doctor,Receptionist")]
    public async Task<ActionResult<PatientDto>> GetPatient(
        Guid id,
        CancellationToken cancellationToken)
        => Ok(await _patientService.GetPatientByIdAsync(id, cancellationToken));
}
