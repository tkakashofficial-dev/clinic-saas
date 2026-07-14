using Clinic.Domain.Common;
using Clinic.Domain.Enums;

namespace Clinic.Domain.Entities;

/// <summary>
/// One stock item in the clinic's pharmacy/store — a medicine, consumable
/// (gloves, anesthetic cartridges) or small equipment. Quantity moves via
/// AdjustStock so it can never go negative.
/// </summary>
public class InventoryItem : BaseEntity, IMustHaveTenant
{
    public Guid TenantId { get; private set; }

    public string Name { get; private set; } = default!;
    public InventoryCategory Category { get; private set; }
    /// <summary>How this item is counted: strip, bottle, box, piece…</summary>
    public string Unit { get; private set; } = default!;
    public int StockQuantity { get; private set; }
    /// <summary>At or below this the item shows as "low stock" — time to reorder.</summary>
    public int ReorderLevel { get; private set; }
    public decimal? UnitPriceRupees { get; private set; }
    public DateOnly? ExpiryDate { get; private set; }

    public Tenant Tenant { get; private set; } = default!;

    private InventoryItem() { }

    public InventoryItem(
        Guid tenantId,
        string name,
        InventoryCategory category,
        string unit,
        int stockQuantity,
        int reorderLevel,
        decimal? unitPriceRupees = null,
        DateOnly? expiryDate = null)
    {
        if (stockQuantity < 0) throw new ArgumentOutOfRangeException(nameof(stockQuantity));
        if (reorderLevel < 0) throw new ArgumentOutOfRangeException(nameof(reorderLevel));

        TenantId = tenantId;
        Name = name ?? throw new ArgumentNullException(nameof(name));
        Category = category;
        Unit = unit ?? throw new ArgumentNullException(nameof(unit));
        StockQuantity = stockQuantity;
        ReorderLevel = reorderLevel;
        UnitPriceRupees = unitPriceRupees;
        ExpiryDate = expiryDate;
    }

    public void Update(
        string name,
        InventoryCategory category,
        string unit,
        int reorderLevel,
        decimal? unitPriceRupees,
        DateOnly? expiryDate)
    {
        if (reorderLevel < 0) throw new ArgumentOutOfRangeException(nameof(reorderLevel));

        Name = name ?? throw new ArgumentNullException(nameof(name));
        Category = category;
        Unit = unit ?? throw new ArgumentNullException(nameof(unit));
        ReorderLevel = reorderLevel;
        UnitPriceRupees = unitPriceRupees;
        ExpiryDate = expiryDate;
    }

    /// <summary>Positive delta = stock in (purchase), negative = stock out
    /// (dispensed/damaged/expired). Stock can never go below zero.</summary>
    public void AdjustStock(int delta)
    {
        var next = StockQuantity + delta;
        if (next < 0)
            throw new InvalidOperationException(
                $"Only {StockQuantity} in stock — cannot remove {-delta}.");
        StockQuantity = next;
    }
}
