namespace Clinic.Application.Features.Invoices.DTOs;

public class InvoiceDto
{
    public Guid Id { get; set; }
    /// <summary>Per-clinic number, shown as INV-000042.</summary>
    public int InvoiceNumber { get; set; }
    public Guid PatientId { get; set; }
    public string PatientName { get; set; } = default!;
    public string PatientPhone { get; set; } = default!;
    public string Status { get; set; } = default!;
    public List<InvoiceItemDto> Items { get; set; } = new();
    public decimal SubtotalRupees { get; set; }
    public decimal DiscountRupees { get; set; }
    public decimal TotalRupees { get; set; }
    public string? PaymentMethod { get; set; }
    public DateTime? PaidAt { get; set; }
    public string? Notes { get; set; }
    public string CreatedByName { get; set; } = default!;
    public DateTime CreatedAt { get; set; }
}

public class InvoiceItemDto
{
    public string Description { get; set; } = default!;
    public int Quantity { get; set; }
    public decimal UnitPriceRupees { get; set; }
    public decimal LineTotalRupees { get; set; }
}

public class CreateInvoiceRequest
{
    public Guid PatientId { get; set; }
    public List<CreateInvoiceItem> Items { get; set; } = new();
    public decimal DiscountRupees { get; set; }
    public string? Notes { get; set; }
    /// <summary>Cash-at-counter flow: create and collect in one step.</summary>
    public bool MarkPaid { get; set; }
    public string? PaymentMethod { get; set; }
}

public class CreateInvoiceItem
{
    public string Description { get; set; } = default!;
    public int Quantity { get; set; } = 1;
    public decimal UnitPriceRupees { get; set; }
}

public class MarkInvoicePaidRequest
{
    public string PaymentMethod { get; set; } = default!;
}

/// <summary>The money pulse the front desk and owner check daily.</summary>
public class InvoiceStatsDto
{
    public decimal TodayCollectedRupees { get; set; }
    public decimal MonthCollectedRupees { get; set; }
    public int UnpaidCount { get; set; }
    public decimal UnpaidTotalRupees { get; set; }
}
