namespace Clinic.Domain.Enums;

/// <summary>What kind of stock item this is — drives pharmacy vs supplies views.</summary>
public enum InventoryCategory
{
    Medicine = 1,
    Consumable = 2,
    Equipment = 3
}
