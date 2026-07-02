using Clinic.Application.Features.Consultations.Services;
using Clinic.Domain.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Clinic.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class PrescriptionController : ControllerBase
{
    private readonly IConsultationService _consultationService;

    public PrescriptionController(IConsultationService consultationService)
    {
        _consultationService = consultationService;
    }

    /// <summary>Downloads the prescription as a branded PDF.</summary>
    [HttpGet("{id}/pdf")]
    [Authorize(Roles = $"{RoleNames.Admin},{RoleNames.Doctor},{RoleNames.Receptionist}")]
    public async Task<IActionResult> GetPdf(Guid id, CancellationToken cancellationToken)
    {
        var (content, fileName) = await _consultationService
            .GetPrescriptionPdfAsync(id, cancellationToken);
        return File(content, "application/pdf", fileName);
    }
}
