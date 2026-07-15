using Clinic.Application.Features.Platform.DTOs;
using Clinic.Application.Features.Platform.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Clinic.Api.Controllers;

/// <summary>
/// The SaaS owner's console. [Authorize] gets them authenticated;
/// the service enforces the platform-admin email allowlist on every call.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class PlatformController : ControllerBase
{
    private readonly IPlatformService _platformService;

    public PlatformController(IPlatformService platformService)
    {
        _platformService = platformService;
    }

    [HttpGet("tenants")]
    public async Task<ActionResult<List<PlatformTenantDto>>> Tenants(CancellationToken cancellationToken)
        => Ok(await _platformService.GetTenantsAsync(cancellationToken));

    [HttpPost("tenants/{id}/change-plan")]
    public async Task<ActionResult<PlatformTenantDto>> ChangePlan(
        Guid id, [FromBody] PlatformChangePlanRequest request, CancellationToken cancellationToken)
        => Ok(await _platformService.ChangePlanAsync(id, request, cancellationToken));

    [HttpPost("tenants/{id}/set-active")]
    public async Task<ActionResult<PlatformTenantDto>> SetActive(
        Guid id, [FromBody] PlatformSetActiveRequest request, CancellationToken cancellationToken)
        => Ok(await _platformService.SetActiveAsync(id, request, cancellationToken));

    /// <summary>Records a collected payment (UPI/bank/cash) and extends coverage.</summary>
    [HttpPost("tenants/{id}/record-payment")]
    public async Task<ActionResult<PlatformTenantDto>> RecordPayment(
        Guid id, [FromBody] RecordPaymentRequest request, CancellationToken cancellationToken)
        => Ok(await _platformService.RecordPaymentAsync(id, request, cancellationToken));

    /// <summary>Proves production SMTP works: sends a real mail to the caller.</summary>
    [HttpPost("test-email")]
    public async Task<ActionResult<PlatformEmailTestResult>> TestEmail(CancellationToken cancellationToken)
        => Ok(await _platformService.SendTestEmailAsync(cancellationToken));
}
