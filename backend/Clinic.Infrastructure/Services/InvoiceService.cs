using Clinic.Application.Common.Exceptions;
using Clinic.Application.Common.Interfaces;
using Clinic.Application.Common.Models;
using Clinic.Application.Features.Invoices.DTOs;
using Clinic.Application.Features.Invoices.Services;
using Clinic.Domain.Constants;
using Clinic.Domain.Entities;
using Clinic.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Clinic.Infrastructure.Services;

public class InvoiceService : IInvoiceService
{
    private readonly ClinicDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public InvoiceService(ClinicDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<PagedResult<InvoiceDto>> GetAllAsync(
        string? status, string? search, PageRequest page,
        CancellationToken cancellationToken = default)
    {
        var query = _context.Invoices
            .AsNoTracking()
            .Include(i => i.Items)
            .Include(i => i.Patient)
            .Include(i => i.CreatedByUser).ThenInclude(tu => tu.SystemUser)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(status) && status != "All")
            query = query.Where(i => i.Status == status);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            query = query.Where(i =>
                (i.Patient.FirstName + " " + i.Patient.LastName).ToLower().Contains(term)
                || i.Patient.Phone.Contains(term));
        }

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(i => i.CreatedAt)
            .Skip((page.NormalizedPage - 1) * page.NormalizedPageSize)
            .Take(page.NormalizedPageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<InvoiceDto>
        {
            Items = items.Select(MapToDto).ToList(),
            Page = page.NormalizedPage,
            PageSize = page.NormalizedPageSize,
            TotalCount = total,
        };
    }

    public async Task<InvoiceDto> GetByIdAsync(
        Guid invoiceId, CancellationToken cancellationToken = default)
        => MapToDto(await GetInvoiceAsync(invoiceId, cancellationToken));

    public async Task<InvoiceDto> CreateAsync(
        CreateInvoiceRequest request, CancellationToken cancellationToken = default)
    {
        if (request.Items.Count == 0)
            throw new BadRequestException("Add at least one item to the invoice.");
        if (request.Items.Count > 30)
            throw new BadRequestException("Maximum 30 items per invoice.");
        if (request.DiscountRupees < 0)
            throw new BadRequestException("Discount cannot be negative.");
        foreach (var item in request.Items)
        {
            if (string.IsNullOrWhiteSpace(item.Description))
                throw new BadRequestException("Every item needs a description.");
            if (item.Quantity is < 1 or > 999)
                throw new BadRequestException("Item quantity must be between 1 and 999.");
            if (item.UnitPriceRupees is < 0 or > 10_000_000)
                throw new BadRequestException("Item price looks wrong — check the amount.");
        }
        if (request.MarkPaid && !PaymentMethods.All.Contains(request.PaymentMethod ?? ""))
            throw new BadRequestException(
                $"Payment method must be one of: {string.Join(", ", PaymentMethods.All)}.");

        var patientExists = await _context.Patients
            .AnyAsync(p => p.Id == request.PatientId, cancellationToken);
        if (!patientExists) throw new NotFoundException("Patient not found.");

        var invoice = new Invoice(
            _currentUser.TenantId, request.PatientId, _currentUser.TenantUserId,
            request.DiscountRupees,
            string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim());

        try
        {
            foreach (var item in request.Items)
                invoice.AddItem(item.Description.Trim(), item.Quantity, item.UnitPriceRupees);
        }
        catch (InvalidOperationException ex)
        {
            throw new BadRequestException(ex.Message);   // discount > subtotal
        }

        // Per-clinic sequence; the unique index is the safety net for races
        var nextNumber = await _context.Invoices
            .Where(i => i.TenantId == _currentUser.TenantId)
            .Select(i => (int?)i.InvoiceNumber)
            .MaxAsync(cancellationToken) ?? 0;
        invoice.AssignNumber(nextNumber + 1);

        if (request.MarkPaid) invoice.MarkPaid(request.PaymentMethod!);

        _context.Invoices.Add(invoice);
        await _context.SaveChangesAsync(cancellationToken);

        return MapToDto(await GetInvoiceAsync(invoice.Id, cancellationToken));
    }

    public async Task<InvoiceDto> MarkPaidAsync(
        Guid invoiceId, MarkInvoicePaidRequest request, CancellationToken cancellationToken = default)
    {
        if (!PaymentMethods.All.Contains(request.PaymentMethod))
            throw new BadRequestException(
                $"Payment method must be one of: {string.Join(", ", PaymentMethods.All)}.");

        var invoice = await GetInvoiceAsync(invoiceId, cancellationToken, track: true);
        try
        {
            invoice.MarkPaid(request.PaymentMethod);
        }
        catch (InvalidOperationException ex)
        {
            throw new BadRequestException(ex.Message);
        }
        await _context.SaveChangesAsync(cancellationToken);
        return MapToDto(invoice);
    }

    public async Task<InvoiceDto> CancelAsync(
        Guid invoiceId, CancellationToken cancellationToken = default)
    {
        var invoice = await GetInvoiceAsync(invoiceId, cancellationToken, track: true);
        try
        {
            invoice.Cancel();
        }
        catch (InvalidOperationException ex)
        {
            throw new BadRequestException(ex.Message);
        }
        await _context.SaveChangesAsync(cancellationToken);
        return MapToDto(invoice);
    }

    public async Task<InvoiceStatsDto> GetStatsAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var todayStart = now.Date;
        var monthStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);

        var paid = _context.Invoices.AsNoTracking()
            .Where(i => i.Status == InvoiceStatuses.Paid && i.PaidAt != null);

        return new InvoiceStatsDto
        {
            TodayCollectedRupees = await paid
                .Where(i => i.PaidAt >= todayStart)
                .SumAsync(i => (decimal?)i.TotalRupees, cancellationToken) ?? 0,
            MonthCollectedRupees = await paid
                .Where(i => i.PaidAt >= monthStart)
                .SumAsync(i => (decimal?)i.TotalRupees, cancellationToken) ?? 0,
            UnpaidCount = await _context.Invoices
                .CountAsync(i => i.Status == InvoiceStatuses.Unpaid, cancellationToken),
            UnpaidTotalRupees = await _context.Invoices
                .Where(i => i.Status == InvoiceStatuses.Unpaid)
                .SumAsync(i => (decimal?)i.TotalRupees, cancellationToken) ?? 0,
        };
    }

    public async Task<(byte[] Content, string FileName)> GetPdfAsync(
        Guid invoiceId, CancellationToken cancellationToken = default)
    {
        var invoice = await GetInvoiceAsync(invoiceId, cancellationToken);
        var tenant = await _context.Tenants
            .AsNoTracking()
            .FirstAsync(t => t.Id == _currentUser.TenantId, cancellationToken);

        var pdf = InvoicePdfGenerator.Generate(tenant.Name, tenant.Address, tenant.Phone, invoice);
        return (pdf, $"invoice-INV{invoice.InvoiceNumber:D6}.pdf");
    }

    public async Task<DuesReportDto> GetDuesAsync(CancellationToken cancellationToken = default)
    {
        // One row per patient with unpaid bills, biggest debtor first —
        // grouped in SQL so it stays fast at thousands of invoices
        var rows = await _context.Invoices
            .AsNoTracking()
            .Where(i => i.Status == InvoiceStatuses.Unpaid)
            .GroupBy(i => new
            {
                i.PatientId,
                i.Patient.FirstName,
                i.Patient.LastName,
                i.Patient.Phone,
            })
            .Select(g => new PatientDuesDto
            {
                PatientId = g.Key.PatientId,
                PatientName = g.Key.FirstName + " " + g.Key.LastName,
                PatientPhone = g.Key.Phone,
                UnpaidCount = g.Count(),
                OutstandingRupees = g.Sum(i => i.TotalRupees),
                OldestUnpaidAt = g.Min(i => i.CreatedAt),
            })
            .OrderByDescending(r => r.OutstandingRupees)
            .ToListAsync(cancellationToken);

        return new DuesReportDto
        {
            TotalOutstandingRupees = rows.Sum(r => r.OutstandingRupees),
            PatientsWithDues = rows.Count,
            Rows = rows,
        };
    }

    public async Task<string> ExportCsvAsync(CancellationToken cancellationToken = default)
    {
        var invoices = await _context.Invoices
            .AsNoTracking()
            .Include(i => i.Items)
            .Include(i => i.Patient)
            .OrderBy(i => i.InvoiceNumber)
            .ToListAsync(cancellationToken);

        var sb = new System.Text.StringBuilder();
        sb.AppendLine(CsvUtil.Row(
            "Invoice", "Date", "Patient", "Phone", "Items",
            "SubtotalRupees", "DiscountRupees", "TotalRupees",
            "Status", "PaymentMethod", "PaidAt", "Notes"));

        foreach (var i in invoices)
            sb.AppendLine(CsvUtil.Row(
                $"INV-{i.InvoiceNumber:D6}",
                i.CreatedAt.ToString("yyyy-MM-dd"),
                $"{i.Patient.FirstName} {i.Patient.LastName}",
                i.Patient.Phone,
                string.Join("; ", i.Items.Select(x =>
                    $"{x.Description} x{x.Quantity} @{x.UnitPriceRupees}")),
                i.SubtotalRupees.ToString("0.##"),
                i.DiscountRupees.ToString("0.##"),
                i.TotalRupees.ToString("0.##"),
                i.Status,
                i.PaymentMethod,
                i.PaidAt?.ToString("yyyy-MM-dd HH:mm"),
                i.Notes));

        return sb.ToString();
    }

    private async Task<Invoice> GetInvoiceAsync(
        Guid invoiceId, CancellationToken ct, bool track = false)
    {
        var query = _context.Invoices
            .Include(i => i.Items)
            .Include(i => i.Patient)
            .Include(i => i.CreatedByUser).ThenInclude(tu => tu.SystemUser)
            .AsQueryable();
        if (!track) query = query.AsNoTracking();

        return await query.FirstOrDefaultAsync(i => i.Id == invoiceId, ct)
            ?? throw new NotFoundException("Invoice not found.");
    }

    private static InvoiceDto MapToDto(Invoice invoice) => new()
    {
        Id = invoice.Id,
        InvoiceNumber = invoice.InvoiceNumber,
        PatientId = invoice.PatientId,
        PatientName = $"{invoice.Patient.FirstName} {invoice.Patient.LastName}",
        PatientPhone = invoice.Patient.Phone,
        Status = invoice.Status,
        Items = invoice.Items.Select(i => new InvoiceItemDto
        {
            Description = i.Description,
            Quantity = i.Quantity,
            UnitPriceRupees = i.UnitPriceRupees,
            LineTotalRupees = i.LineTotalRupees,
        }).ToList(),
        SubtotalRupees = invoice.SubtotalRupees,
        DiscountRupees = invoice.DiscountRupees,
        TotalRupees = invoice.TotalRupees,
        PaymentMethod = invoice.PaymentMethod,
        PaidAt = invoice.PaidAt,
        Notes = invoice.Notes,
        CreatedByName = $"{invoice.CreatedByUser.SystemUser.FirstName} {invoice.CreatedByUser.SystemUser.LastName}",
        CreatedAt = invoice.CreatedAt,
    };
}
