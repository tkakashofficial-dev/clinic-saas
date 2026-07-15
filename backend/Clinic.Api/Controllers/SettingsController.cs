using Clinic.Application.Features.Settings.DTOs;
using Clinic.Application.Features.Settings.Services;
using Clinic.Domain.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Clinic.Api.Controllers;

/// <summary>
/// Clinic settings: the clinic's letterhead (name/phone/address on every
/// PDF) and template preferences. Everyone can read; only Admin writes.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class SettingsController : ControllerBase
{
    private readonly IClinicSettingsService _settingsService;

    public SettingsController(IClinicSettingsService settingsService)
    {
        _settingsService = settingsService;
    }

    [HttpGet]
    [Authorize(Roles = $"{RoleNames.Admin},{RoleNames.Doctor},{RoleNames.Receptionist}")]
    public async Task<ActionResult<ClinicSettingsDto>> Get(CancellationToken cancellationToken)
        => Ok(await _settingsService.GetAsync(cancellationToken));

    [HttpPut]
    [Authorize(Roles = RoleNames.Admin)]
    public async Task<ActionResult<ClinicSettingsDto>> Update(
        [FromBody] UpdateClinicSettingsRequest request, CancellationToken cancellationToken)
        => Ok(await _settingsService.UpdateAsync(request, cancellationToken));
}
