using Clinic.Domain.Common;

namespace Clinic.Domain.Entities;

/// <summary>
/// A patient bill: line items, optional discount, paid/unpaid state.
/// Numbered per clinic (INV-000123) like patient files. Totals are computed
/// and stored server-side — the client never dictates money math.
/// </summary>
public class Invoice : BaseEntity, IMustHaveTenant
{
    public Guid TenantId { get; private set; }
    /// <summary>Human-friendly per-clinic number (INV-000042).</summary>
    public int InvoiceNumber { get; private set; }
    public Guid PatientId { get; private set; }
    public Guid CreatedByTenantUserId { get; private set; }

    /// <summary>InvoiceStatuses: Unpaid, Paid, Cancelled.</summary>
    public string Status { get; private set; } = default!;
    public decimal SubtotalRupees { get; private set; }
    public decimal DiscountRupees { get; private set; }
    public decimal TotalRupees { get; private set; }
    /// <summary>PaymentMethods constant — set when paid.</summary>
    public string? PaymentMethod { get; private set; }
    public DateTime? PaidAt { get; private set; }
    public string? Notes { get; private set; }

    public Patient Patient { get; private set; } = default!;
    public TenantUser CreatedByUser { get; private set; } = default!;

    private readonly List<InvoiceItem> _items = new();
    public IReadOnlyCollection<InvoiceItem> Items => _items;

    private Invoice() { }

    public Invoice(
        Guid tenantId, Guid patientId, Guid createdByTenantUserId,
        decimal discountRupees, string? notes)
    {
        TenantId = tenantId;
        PatientId = patientId;
        CreatedByTenantUserId = createdByTenantUserId;
        Status = Constants.InvoiceStatuses.Unpaid;
        DiscountRupees = discountRupees;
        Notes = notes;
    }

    public void AssignNumber(int invoiceNumber) => InvoiceNumber = invoiceNumber;

    public void AddItem(string description, int quantity, decimal unitPriceRupees)
    {
        _items.Add(new InvoiceItem(Id, description, quantity, unitPriceRupees));
        RecalculateTotals();
    }

    private void RecalculateTotals()
    {
        SubtotalRupees = _items.Sum(i => i.LineTotalRupees);
        if (DiscountRupees > SubtotalRupees)
            throw new InvalidOperationException("Discount cannot exceed the subtotal.");
        TotalRupees = SubtotalRupees - DiscountRupees;
    }

    public void MarkPaid(string paymentMethod)
    {
        if (Status == Constants.InvoiceStatuses.Cancelled)
            throw new InvalidOperationException("A cancelled invoice cannot be paid.");
        Status = Constants.InvoiceStatuses.Paid;
        PaymentMethod = paymentMethod;
        PaidAt = DateTime.UtcNow;
    }

    public void Cancel()
    {
        if (Status == Constants.InvoiceStatuses.Paid)
            throw new InvalidOperationException(
                "A paid invoice cannot be cancelled — record a correction instead.");
        Status = Constants.InvoiceStatuses.Cancelled;
    }
}

/// <summary>One billed line: consultation, filling, X-ray, medicines…</summary>
public class InvoiceItem : BaseEntity
{
    public Guid InvoiceId { get; private set; }
    public string Description { get; private set; } = default!;
    public int Quantity { get; private set; }
    public decimal UnitPriceRupees { get; private set; }
    public decimal LineTotalRupees { get; private set; }

    private InvoiceItem() { }

    public InvoiceItem(Guid invoiceId, string description, int quantity, decimal unitPriceRupees)
    {
        if (quantity <= 0) throw new ArgumentOutOfRangeException(nameof(quantity));
        if (unitPriceRupees < 0) throw new ArgumentOutOfRangeException(nameof(unitPriceRupees));

        InvoiceId = invoiceId;
        Description = description ?? throw new ArgumentNullException(nameof(description));
        Quantity = quantity;
        UnitPriceRupees = unitPriceRupees;
        LineTotalRupees = quantity * unitPriceRupees;
    }
}
