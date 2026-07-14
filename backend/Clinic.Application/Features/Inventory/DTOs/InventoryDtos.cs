namespace Clinic.Application.Features.Inventory.DTOs;

public class InventoryItemDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = default!;
    public string Category { get; set; } = default!;
    public string Unit { get; set; } = default!;
    public int StockQuantity { get; set; }
    public int ReorderLevel { get; set; }
    /// <summary>At/below reorder level — the "order more" signal.</summary>
    public bool LowStock { get; set; }
    public decimal? UnitPriceRupees { get; set; }
    public DateOnly? ExpiryDate { get; set; }
    /// <summary>Expiry within 60 days (or past) — flag prominently.</summary>
    public bool ExpiringSoon { get; set; }
}

/// <summary>Create/update payload — stock itself moves via adjust-stock.</summary>
public class SaveInventoryItemRequest
{
    public string Name { get; set; } = default!;
    public string Category { get; set; } = default!;
    public string Unit { get; set; } = default!;
    /// <summary>Opening stock — only honored on create.</summary>
    public int StockQuantity { get; set; }
    public int ReorderLevel { get; set; }
    public decimal? UnitPriceRupees { get; set; }
    public DateOnly? ExpiryDate { get; set; }
}

public class AdjustStockRequest
{
    /// <summary>Positive = stock in (purchase), negative = stock out (dispensed/damaged).</summary>
    public int Delta { get; set; }
}
