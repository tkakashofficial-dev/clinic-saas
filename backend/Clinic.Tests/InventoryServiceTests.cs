using Clinic.Application.Common.Exceptions;
using Clinic.Application.Features.Inventory.DTOs;
using Clinic.Infrastructure.Services;
using Clinic.Tests.TestInfrastructure;
using Microsoft.EntityFrameworkCore;

namespace Clinic.Tests;

/// <summary>
/// Pharmacy & stores: stock can never go negative, item names are unique per
/// clinic, low-stock flags fire at the reorder level, and one clinic can
/// never see another clinic's shelves.
/// </summary>
public class InventoryServiceTests : IDisposable
{
    private readonly TestDb _db = new();

    private InventoryService CreateService() =>
        new(_db.CreateContext(), _db.CurrentUser);

    private static SaveInventoryItemRequest Amoxicillin(int stock = 40, int reorder = 10) => new()
    {
        Name = "Amoxicillin 500mg",
        Category = "Medicine",
        Unit = "strip",
        StockQuantity = stock,
        ReorderLevel = reorder,
        UnitPriceRupees = 85
    };

    [Fact]
    public async Task Create_ThenList_ShowsItemWithStock()
    {
        var clinic = await _db.SeedTenantAsync("Smile Dental", "a@clinic.com");
        _db.CurrentUser.ActAs(clinic.TenantId, clinic.TenantUserId);

        await CreateService().CreateAsync(Amoxicillin());
        var items = await CreateService().GetAllAsync(null);

        var item = Assert.Single(items);
        Assert.Equal("Amoxicillin 500mg", item.Name);
        Assert.Equal(40, item.StockQuantity);
        Assert.False(item.LowStock);
    }

    [Fact]
    public async Task Create_DuplicateName_IsRejected()
    {
        var clinic = await _db.SeedTenantAsync("Smile Dental", "a@clinic.com");
        _db.CurrentUser.ActAs(clinic.TenantId, clinic.TenantUserId);

        await CreateService().CreateAsync(Amoxicillin());
        await Assert.ThrowsAsync<ConflictException>(
            () => CreateService().CreateAsync(Amoxicillin()));
    }

    [Fact]
    public async Task AdjustStock_DispensingMoreThanStock_IsRejected()
    {
        var clinic = await _db.SeedTenantAsync("Smile Dental", "a@clinic.com");
        _db.CurrentUser.ActAs(clinic.TenantId, clinic.TenantUserId);
        var item = await CreateService().CreateAsync(Amoxicillin(stock: 5));

        await Assert.ThrowsAsync<BadRequestException>(
            () => CreateService().AdjustStockAsync(item.Id, new AdjustStockRequest { Delta = -6 }));

        // A legal dispense right at the reorder level flips the low-stock flag
        var updated = await CreateService().AdjustStockAsync(
            item.Id, new AdjustStockRequest { Delta = -3 });
        Assert.Equal(2, updated.StockQuantity);
        Assert.True(updated.LowStock);
    }

    [Fact]
    public async Task Inventory_IsInvisibleToOtherClinics()
    {
        var clinicA = await _db.SeedTenantAsync("Smile Dental", "a@clinic.com");
        var clinicB = await _db.SeedTenantAsync("City Care", "b@clinic.com");

        _db.CurrentUser.ActAs(clinicA.TenantId, clinicA.TenantUserId);
        await CreateService().CreateAsync(Amoxicillin());

        _db.CurrentUser.ActAs(clinicB.TenantId, clinicB.TenantUserId);
        Assert.Empty(await CreateService().GetAllAsync(null));
        // And clinic B can even reuse the same name for its own shelf
        var own = await CreateService().CreateAsync(Amoxicillin(stock: 3));
        Assert.Equal(3, own.StockQuantity);
    }

    [Fact]
    public async Task Inventory_OnSoloPlan_IsAPaidUpgrade()
    {
        // Inventory is the Clinic tier's headline feature — Solo gets a 402
        // wall on the module, but prescription suggestions stay quiet (no
        // errors mid-typing), they just return nothing.
        var clinic = await _db.SeedTenantAsync("Solo Dental", "solo@clinic.com");
        _db.CurrentUser.ActAs(clinic.TenantId, clinic.TenantUserId);

        await using (var context = _db.CreateContext())
        {
            var tenant = await context.Tenants.FirstAsync(t => t.Id == clinic.TenantId);
            tenant.ChangePlan(Clinic.Domain.Enums.PlanType.Solo);  // ends trial too
            await context.SaveChangesAsync();
        }

        await Assert.ThrowsAsync<PlanLimitException>(() => CreateService().GetAllAsync(null));
        await Assert.ThrowsAsync<PlanLimitException>(() => CreateService().CreateAsync(Amoxicillin()));
        Assert.Empty(await CreateService().SuggestMedicinesAsync("amo"));
    }

    [Fact]
    public async Task SuggestMedicines_MatchesOnlyMedicines()
    {
        var clinic = await _db.SeedTenantAsync("Smile Dental", "a@clinic.com");
        _db.CurrentUser.ActAs(clinic.TenantId, clinic.TenantUserId);

        await CreateService().CreateAsync(Amoxicillin());
        await CreateService().CreateAsync(new SaveInventoryItemRequest
        {
            Name = "Amalgam capsules",       // consumable that also starts with "am"
            Category = "Consumable",
            Unit = "box",
            StockQuantity = 10,
            ReorderLevel = 2
        });

        var suggestions = await CreateService().SuggestMedicinesAsync("amo");

        Assert.Equal(["Amoxicillin 500mg"], suggestions);
    }

    public void Dispose() => _db.Dispose();
}
