using Clinic.Application.Features.PublicBooking.DTOs;
using Clinic.Application.Features.PublicBooking.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Clinic.Api.Controllers;

/// <summary>
/// The patient-facing booking page (/book/{slug} on the frontend). No auth:
/// patients are anonymous visitors. The service pins every query to the slug's
/// tenant explicitly and exposes public letterhead info only.
/// </summary>
[ApiController]
[Route("api/public")]
[AllowAnonymous]
public class PublicController : ControllerBase
{
    private readonly IPublicBookingService _publicBooking;

    public PublicController(IPublicBookingService publicBooking)
    {
        _publicBooking = publicBooking;
    }

    [HttpGet("{slug}")]
    public async Task<ActionResult<PublicClinicDto>> GetClinic(
        string slug, CancellationToken cancellationToken)
        => Ok(await _publicBooking.GetClinicAsync(slug, cancellationToken));

    [HttpPost("{slug}/book")]
    public async Task<ActionResult<PublicBookingResultDto>> Book(
        string slug, [FromBody] PublicBookingRequest request,
        CancellationToken cancellationToken)
        => Ok(await _publicBooking.BookAsync(slug, request, cancellationToken));
}
