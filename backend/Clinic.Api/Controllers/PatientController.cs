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

    /// <summary>Every patient as CSV — the "your data is yours" promise.</summary>
    [HttpGet("export.csv")]
    [Authorize(Roles = RoleNames.Admin)]
    public async Task<IActionResult> ExportCsv(CancellationToken cancellationToken)
    {
        var csv = await _patientService.ExportCsvAsync(cancellationToken);
        // BOM so Excel opens it as UTF-8 (Indian names survive intact)
        var bytes = System.Text.Encoding.UTF8.GetPreamble()
            .Concat(System.Text.Encoding.UTF8.GetBytes(csv)).ToArray();
        return File(bytes, "text/csv", $"patients-{DateTime.UtcNow:yyyy-MM-dd}.csv");
    }

    /// <summary>Bulk CSV import for paper/Excel migration. Needs FirstName +
    /// Phone columns; everything else is best-effort.</summary>
    [HttpPost("import")]
    [Authorize(Roles = RoleNames.Admin)]
    [RequestSizeLimit(2 * 1024 * 1024)]   // 2 MB ≈ far beyond 2,000 rows
    public async Task<ActionResult<ImportResultDto>> ImportCsv(
        IFormFile file, CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
            return BadRequest(new { message = "Choose a CSV file to import." });

        using var reader = new StreamReader(file.OpenReadStream());
        var text = await reader.ReadToEndAsync(cancellationToken);
        return Ok(await _patientService.ImportCsvAsync(text, cancellationToken));
    }

    /// <summary>Seeded medical-condition list for the register/edit tick-boxes.
    /// Static per deployment — the frontend may cache it for the session.</summary>
    [HttpGet("medical-conditions")]
    [Authorize(Roles = $"{RoleNames.Admin},{RoleNames.Doctor},{RoleNames.Receptionist}")]
    public async Task<ActionResult<List<MedicalConditionDto>>> GetMedicalConditions(
        CancellationToken cancellationToken)
        => Ok(await _patientService.GetMedicalConditionsAsync(cancellationToken));

    /// <summary>The patient's clinical story — every consultation, newest first.</summary>
    [HttpGet("{id}/history")]
    [Authorize(Roles = $"{RoleNames.Admin},{RoleNames.Doctor},{RoleNames.Receptionist}")]
    public async Task<ActionResult<PatientHistoryDto>> GetHistory(
        Guid id,
        CancellationToken cancellationToken)
        => Ok(await _patientService.GetHistoryAsync(id, cancellationToken));

    /// <summary>Printable clinic-branded intake form, pre-filled with patient data.
    /// ?template=dental|general picks one of the seeded designs.</summary>
    [HttpGet("{id}/intake-form")]
    [Authorize(Roles = $"{RoleNames.Admin},{RoleNames.Doctor},{RoleNames.Receptionist}")]
    public async Task<IActionResult> GetIntakeForm(
        Guid id, [FromQuery] string template = "dental", CancellationToken cancellationToken = default)
    {
        if (template is not ("dental" or "general"))
            return BadRequest(new { message = "Unknown template. Available: dental, general." });

        var (content, fileName) = await _patientService.GetIntakeFormPdfAsync(id, template, cancellationToken);
        return File(content, "application/pdf", fileName);
    }
}
