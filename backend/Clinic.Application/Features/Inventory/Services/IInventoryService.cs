using Clinic.Application.Features.Inventory.DTOs;

namespace Clinic.Application.Features.Inventory.Services;

public interface IInventoryService
{
    /// <summary>All items for this clinic; optional name search. Low-stock first.</summary>
    Task<List<InventoryItemDto>> GetAllAsync(
        string? search, CancellationToken cancellationToken = default);

    Task<InventoryItemDto> CreateAsync(
        SaveInventoryItemRequest request, CancellationToken cancellationToken = default);

    Task<InventoryItemDto> UpdateAsync(
        Guid itemId, SaveInventoryItemRequest request, CancellationToken cancellationToken = default);

    Task<InventoryItemDto> AdjustStockAsync(
        Guid itemId, AdjustStockRequest request, CancellationToken cancellationToken = default);

    Task DeleteAsync(Guid itemId, CancellationToken cancellationToken = default);

    /// <summary>Medicine-name suggestions for the prescription form.</summary>
    Task<List<string>> SuggestMedicinesAsync(
        string query, CancellationToken cancellationToken = default);
}
