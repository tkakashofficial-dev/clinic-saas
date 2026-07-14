using Clinic.Application.Features.Inventory.DTOs;
using Clinic.Application.Features.Inventory.Services;
using Clinic.Domain.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Clinic.Api.Controllers;

/// <summary>
/// Pharmacy & stores: everyone can look up stock (doctors need it while
/// prescribing); reception/admin manage it; only Admin removes items.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class InventoryController : ControllerBase
{
    private readonly IInventoryService _inventoryService;

    public InventoryController(IInventoryService inventoryService)
    {
        _inventoryService = inventoryService;
    }

    [HttpGet]
    [Authorize(Roles = $"{RoleNames.Admin},{RoleNames.Doctor},{RoleNames.Receptionist}")]
    public async Task<ActionResult<List<InventoryItemDto>>> GetAll(
        [FromQuery] string? search, CancellationToken cancellationToken)
        => Ok(await _inventoryService.GetAllAsync(search, cancellationToken));

    /// <summary>Medicine-name autocomplete for the prescription form.</summary>
    [HttpGet("suggest")]
    [Authorize(Roles = $"{RoleNames.Admin},{RoleNames.Doctor},{RoleNames.Receptionist}")]
    public async Task<ActionResult<List<string>>> Suggest(
        [FromQuery] string query, CancellationToken cancellationToken)
        => Ok(await _inventoryService.SuggestMedicinesAsync(query, cancellationToken));

    [HttpPost]
    [Authorize(Roles = $"{RoleNames.Admin},{RoleNames.Receptionist}")]
    public async Task<ActionResult<InventoryItemDto>> Create(
        [FromBody] SaveInventoryItemRequest request, CancellationToken cancellationToken)
        => Ok(await _inventoryService.CreateAsync(request, cancellationToken));

    [HttpPut("{id}")]
    [Authorize(Roles = $"{RoleNames.Admin},{RoleNames.Receptionist}")]
    public async Task<ActionResult<InventoryItemDto>> Update(
        Guid id, [FromBody] SaveInventoryItemRequest request, CancellationToken cancellationToken)
        => Ok(await _inventoryService.UpdateAsync(id, request, cancellationToken));

    [HttpPost("{id}/adjust-stock")]
    [Authorize(Roles = $"{RoleNames.Admin},{RoleNames.Receptionist}")]
    public async Task<ActionResult<InventoryItemDto>> AdjustStock(
        Guid id, [FromBody] AdjustStockRequest request, CancellationToken cancellationToken)
        => Ok(await _inventoryService.AdjustStockAsync(id, request, cancellationToken));

    [HttpDelete("{id}")]
    [Authorize(Roles = RoleNames.Admin)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await _inventoryService.DeleteAsync(id, cancellationToken);
        return NoContent();
    }
}
