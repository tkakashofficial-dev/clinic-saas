using Clinic.Application.Common.Models;
using Clinic.Application.Features.Invoices.DTOs;
using Clinic.Application.Features.Invoices.Services;
using Clinic.Domain.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Clinic.Api.Controllers;

/// <summary>
/// Patient billing. Money moves at the front desk: Admin/Reception create,
/// collect and cancel; doctors can look invoices up.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class InvoicesController : ControllerBase
{
    private readonly IInvoiceService _invoiceService;

    public InvoicesController(IInvoiceService invoiceService)
    {
        _invoiceService = invoiceService;
    }

    [HttpGet]
    [Authorize(Roles = $"{RoleNames.Admin},{RoleNames.Doctor},{RoleNames.Receptionist}")]
    public async Task<ActionResult<PagedResult<InvoiceDto>>> GetAll(
        [FromQuery] string? status,
        [FromQuery] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = PageRequest.DefaultPageSize,
        CancellationToken cancellationToken = default)
        => Ok(await _invoiceService.GetAllAsync(
            status, search, new PageRequest(page, pageSize), cancellationToken));

    /// <summary>Today's & this month's collections + unpaid backlog.</summary>
    [HttpGet("stats")]
    [Authorize(Roles = $"{RoleNames.Admin},{RoleNames.Receptionist}")]
    public async Task<ActionResult<InvoiceStatsDto>> Stats(CancellationToken cancellationToken)
        => Ok(await _invoiceService.GetStatsAsync(cancellationToken));

    [HttpGet("{id}")]
    [Authorize(Roles = $"{RoleNames.Admin},{RoleNames.Doctor},{RoleNames.Receptionist}")]
    public async Task<ActionResult<InvoiceDto>> GetById(Guid id, CancellationToken cancellationToken)
        => Ok(await _invoiceService.GetByIdAsync(id, cancellationToken));

    [HttpPost]
    [Authorize(Roles = $"{RoleNames.Admin},{RoleNames.Receptionist}")]
    public async Task<ActionResult<InvoiceDto>> Create(
        [FromBody] CreateInvoiceRequest request, CancellationToken cancellationToken)
        => Ok(await _invoiceService.CreateAsync(request, cancellationToken));

    [HttpPost("{id}/mark-paid")]
    [Authorize(Roles = $"{RoleNames.Admin},{RoleNames.Receptionist}")]
    public async Task<ActionResult<InvoiceDto>> MarkPaid(
        Guid id, [FromBody] MarkInvoicePaidRequest request, CancellationToken cancellationToken)
        => Ok(await _invoiceService.MarkPaidAsync(id, request, cancellationToken));

    [HttpPost("{id}/cancel")]
    [Authorize(Roles = $"{RoleNames.Admin},{RoleNames.Receptionist}")]
    public async Task<ActionResult<InvoiceDto>> Cancel(Guid id, CancellationToken cancellationToken)
        => Ok(await _invoiceService.CancelAsync(id, cancellationToken));

    /// <summary>The branded invoice/receipt PDF.</summary>
    [HttpGet("{id}/pdf")]
    [Authorize(Roles = $"{RoleNames.Admin},{RoleNames.Doctor},{RoleNames.Receptionist}")]
    public async Task<IActionResult> Pdf(Guid id, CancellationToken cancellationToken)
    {
        var (content, fileName) = await _invoiceService.GetPdfAsync(id, cancellationToken);
        return File(content, "application/pdf", fileName);
    }
}
