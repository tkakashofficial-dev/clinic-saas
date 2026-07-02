using Clinic.Application.Features.Reports.DTOs;
using Clinic.Application.Features.Reports.Services;
using Clinic.Domain.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Clinic.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = RoleNames.Admin)] // analytics are owner-level information
public class ReportsController : ControllerBase
{
    private readonly IReportsService _reportsService;

    public ReportsController(IReportsService reportsService)
    {
        _reportsService = reportsService;
    }

    [HttpGet("overview")]
    public async Task<ActionResult<PracticeOverviewDto>> Overview(CancellationToken cancellationToken)
        => Ok(await _reportsService.GetPracticeOverviewAsync(cancellationToken));

    /// <summary>The overview as a downloadable branded PDF.</summary>
    [HttpGet("overview/pdf")]
    public async Task<IActionResult> OverviewPdf(CancellationToken cancellationToken)
    {
        var (content, fileName) = await _reportsService.GetPracticeOverviewPdfAsync(cancellationToken);
        return File(content, "application/pdf", fileName);
    }
}
