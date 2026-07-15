using Clinic.Application.Common.Exceptions;
using Clinic.Application.Common.Interfaces;
using Clinic.Application.Features.Inventory.DTOs;
using Clinic.Application.Features.Inventory.Services;
using Clinic.Domain.Constants;
using Clinic.Domain.Entities;
using Clinic.Domain.Enums;
using Clinic.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Clinic.Infrastructure.Services;

public class InventoryService : IInventoryService
{
    private readonly ClinicDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public InventoryService(ClinicDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<List<InventoryItemDto>> GetAllAsync(
        string? search, CancellationToken cancellationToken = default)
    {
        await EnsurePlanAllowsInventoryAsync(cancellationToken);

        var query = _context.InventoryItems.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            query = query.Where(i => i.Name.ToLower().Contains(term));
        }

        var items = await query.OrderBy(i => i.Name).ToListAsync(cancellationToken);

        // Low stock floats to the top — that's what the pharmacist checks daily
        return items
            .Select(MapToDto)
            .OrderByDescending(i => i.LowStock)
            .ThenByDescending(i => i.ExpiringSoon)
            .ThenBy(i => i.Name)
            .ToList();
    }

    public async Task<InventoryItemDto> CreateAsync(
        SaveInventoryItemRequest request, CancellationToken cancellationToken = default)
    {
        await EnsurePlanAllowsInventoryAsync(cancellationToken);
        var (name, category, unit) = ValidateAndParse(request);

        var exists = await _context.InventoryItems
            .AnyAsync(i => i.Name.ToLower() == name.ToLower(), cancellationToken);
        if (exists)
            throw new ConflictException(
                $"'{name}' is already in the inventory — adjust its stock instead of adding it twice.");

        if (request.StockQuantity < 0)
            throw new BadRequestException("Opening stock cannot be negative.");

        var item = new InventoryItem(
            _currentUser.TenantId, name, category, unit,
            request.StockQuantity, request.ReorderLevel,
            request.UnitPriceRupees, request.ExpiryDate);

        _context.InventoryItems.Add(item);
        await _context.SaveChangesAsync(cancellationToken);
        return MapToDto(item);
    }

    public async Task<InventoryItemDto> UpdateAsync(
        Guid itemId, SaveInventoryItemRequest request, CancellationToken cancellationToken = default)
    {
        await EnsurePlanAllowsInventoryAsync(cancellationToken);
        var (name, category, unit) = ValidateAndParse(request);

        var item = await GetItemAsync(itemId, cancellationToken);

        var duplicate = await _context.InventoryItems
            .AnyAsync(i => i.Id != itemId && i.Name.ToLower() == name.ToLower(), cancellationToken);
        if (duplicate)
            throw new ConflictException($"Another item is already named '{name}'.");

        item.Update(name, category, unit, request.ReorderLevel,
            request.UnitPriceRupees, request.ExpiryDate);
        await _context.SaveChangesAsync(cancellationToken);
        return MapToDto(item);
    }

    public async Task<InventoryItemDto> AdjustStockAsync(
        Guid itemId, AdjustStockRequest request, CancellationToken cancellationToken = default)
    {
        await EnsurePlanAllowsInventoryAsync(cancellationToken);

        if (request.Delta == 0)
            throw new BadRequestException("Adjustment cannot be zero.");

        var item = await GetItemAsync(itemId, cancellationToken);

        try
        {
            item.AdjustStock(request.Delta);
        }
        catch (InvalidOperationException ex)
        {
            throw new BadRequestException(ex.Message);
        }

        await _context.SaveChangesAsync(cancellationToken);
        return MapToDto(item);
    }

    public async Task DeleteAsync(Guid itemId, CancellationToken cancellationToken = default)
    {
        await EnsurePlanAllowsInventoryAsync(cancellationToken);
        var item = await GetItemAsync(itemId, cancellationToken);
        item.Delete(_currentUser.UserId);   // soft delete — history stays intact
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<List<string>> SuggestMedicinesAsync(
        string query, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query) || query.Trim().Length < 2)
            return [];

        // Solo plan: no inventory → quietly no suggestions. Throwing here
        // would break the prescription form mid-typing for Solo doctors.
        if (!PlanLimits.HasInventory(await GetEffectivePlanAsync(cancellationToken)))
            return [];

        var term = query.Trim().ToLower();
        return await _context.InventoryItems
            .AsNoTracking()
            .Where(i => i.Category == InventoryCategory.Medicine
                && i.Name.ToLower().Contains(term))
            .OrderBy(i => i.Name)
            .Select(i => i.Name)
            .Take(8)
            .ToListAsync(cancellationToken);
    }

    private async Task<PlanType> GetEffectivePlanAsync(CancellationToken ct)
    {
        var tenant = await _context.Tenants
            .AsNoTracking()
            .FirstAsync(t => t.Id == _currentUser.TenantId, ct);
        return tenant.EffectivePlan;
    }

    /// <summary>Inventory is a Clinic-tier feature: the API is the wall,
    /// the UI shows the beautiful upgrade pitch (HTTP 402).</summary>
    private async Task EnsurePlanAllowsInventoryAsync(CancellationToken ct)
    {
        if (!PlanLimits.HasInventory(await GetEffectivePlanAsync(ct)))
            throw new PlanLimitException(
                "Pharmacy & inventory is included from the Clinic plan. " +
                "Upgrade to track stock, low-stock alerts and expiry warnings.");
    }

    private async Task<InventoryItem> GetItemAsync(Guid itemId, CancellationToken ct)
        => await _context.InventoryItems
               .FirstOrDefaultAsync(i => i.Id == itemId, ct)
           ?? throw new NotFoundException("Inventory item not found.");

    private static (string Name, InventoryCategory Category, string Unit) ValidateAndParse(
        SaveInventoryItemRequest request)
    {
        var name = request.Name?.Trim();
        if (string.IsNullOrWhiteSpace(name))
            throw new BadRequestException("Item name is required.");

        var unit = string.IsNullOrWhiteSpace(request.Unit) ? "piece" : request.Unit.Trim();

        if (!Enum.TryParse<InventoryCategory>(request.Category, true, out var category)
            || !Enum.IsDefined(category))
            throw new BadRequestException(
                $"Unknown category. Available: {string.Join(", ", Enum.GetNames<InventoryCategory>())}.");

        if (request.ReorderLevel < 0)
            throw new BadRequestException("Reorder level cannot be negative.");

        return (name, category, unit);
    }

    private static InventoryItemDto MapToDto(InventoryItem item) => new()
    {
        Id = item.Id,
        Name = item.Name,
        Category = item.Category.ToString(),
        Unit = item.Unit,
        StockQuantity = item.StockQuantity,
        ReorderLevel = item.ReorderLevel,
        LowStock = item.StockQuantity <= item.ReorderLevel,
        UnitPriceRupees = item.UnitPriceRupees,
        ExpiryDate = item.ExpiryDate,
        ExpiringSoon = item.ExpiryDate.HasValue
            && item.ExpiryDate.Value <= DateOnly.FromDateTime(DateTime.UtcNow.AddDays(60))
    };
}
