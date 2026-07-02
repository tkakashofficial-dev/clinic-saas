using Clinic.Application.Features.Billing.DTOs;
using Clinic.Application.Features.Billing.Services;
using Clinic.Domain.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Clinic.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = RoleNames.Admin)] // plans are an owner decision
public class BillingController : ControllerBase
{
    private readonly IBillingService _billingService;

    public BillingController(IBillingService billingService)
    {
        _billingService = billingService;
    }

    [HttpGet("summary")]
    public async Task<ActionResult<BillingSummaryDto>> Summary(CancellationToken cancellationToken)
        => Ok(await _billingService.GetSummaryAsync(cancellationToken));

    [HttpPost("change-plan")]
    public async Task<ActionResult<BillingSummaryDto>> ChangePlan(
        [FromBody] ChangePlanRequest request,
        CancellationToken cancellationToken)
        => Ok(await _billingService.ChangePlanAsync(request, cancellationToken));
}
