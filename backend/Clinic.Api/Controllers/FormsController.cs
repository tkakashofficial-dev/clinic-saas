using Clinic.Application.Features.Forms.DTOs;
using Clinic.Application.Features.Forms.Services;
using Clinic.Domain.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Clinic.Api.Controllers;

/// <summary>
/// The clinic's form builder: custom intake-form sections + template preview.
/// Everyone can read/preview; only Admin designs the forms.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class FormsController : ControllerBase
{
    private readonly IFormsService _formsService;

    public FormsController(IFormsService formsService)
    {
        _formsService = formsService;
    }

    [HttpGet("sections")]
    [Authorize(Roles = $"{RoleNames.Admin},{RoleNames.Doctor},{RoleNames.Receptionist}")]
    public async Task<ActionResult<List<IntakeFormSectionDto>>> GetSections(
        CancellationToken cancellationToken)
        => Ok(await _formsService.GetSectionsAsync(cancellationToken));

    [HttpPost("sections")]
    [Authorize(Roles = RoleNames.Admin)]
    public async Task<ActionResult<IntakeFormSectionDto>> CreateSection(
        [FromBody] SaveIntakeFormSectionRequest request, CancellationToken cancellationToken)
        => Ok(await _formsService.CreateSectionAsync(request, cancellationToken));

    [HttpPut("sections/{id}")]
    [Authorize(Roles = RoleNames.Admin)]
    public async Task<ActionResult<IntakeFormSectionDto>> UpdateSection(
        Guid id, [FromBody] SaveIntakeFormSectionRequest request, CancellationToken cancellationToken)
        => Ok(await _formsService.UpdateSectionAsync(id, request, cancellationToken));

    [HttpDelete("sections/{id}")]
    [Authorize(Roles = RoleNames.Admin)]
    public async Task<IActionResult> DeleteSection(Guid id, CancellationToken cancellationToken)
    {
        await _formsService.DeleteSectionAsync(id, cancellationToken);
        return NoContent();
    }

    /// <summary>direction: -1 = up, 1 = down. Returns the fresh ordering.</summary>
    [HttpPost("sections/{id}/move")]
    [Authorize(Roles = RoleNames.Admin)]
    public async Task<ActionResult<List<IntakeFormSectionDto>>> MoveSection(
        Guid id, [FromQuery] int direction, CancellationToken cancellationToken)
        => Ok(await _formsService.MoveSectionAsync(id, direction, cancellationToken));

    /// <summary>The form with SAMPLE data — see exactly what will print.</summary>
    [HttpGet("preview")]
    [Authorize(Roles = $"{RoleNames.Admin},{RoleNames.Doctor},{RoleNames.Receptionist}")]
    public async Task<IActionResult> Preview(
        [FromQuery] string template = "dental", CancellationToken cancellationToken = default)
    {
        var (content, fileName) = await _formsService.PreviewPdfAsync(template, cancellationToken);
        return File(content, "application/pdf", fileName);
    }
}
