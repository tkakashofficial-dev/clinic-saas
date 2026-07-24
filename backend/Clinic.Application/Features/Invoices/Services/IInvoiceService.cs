using Clinic.Application.Common.Models;
using Clinic.Application.Features.Invoices.DTOs;

namespace Clinic.Application.Features.Invoices.Services;

/// <summary>
/// Patient billing: create itemised bills, collect payment, print branded
/// invoices/receipts. Totals are always computed server-side.
/// </summary>
public interface IInvoiceService
{
    Task<PagedResult<InvoiceDto>> GetAllAsync(
        string? status, string? search, PageRequest page,
        CancellationToken cancellationToken = default);

    Task<InvoiceDto> GetByIdAsync(Guid invoiceId, CancellationToken cancellationToken = default);

    Task<InvoiceDto> CreateAsync(
        CreateInvoiceRequest request, CancellationToken cancellationToken = default);

    Task<InvoiceDto> MarkPaidAsync(
        Guid invoiceId, MarkInvoicePaidRequest request, CancellationToken cancellationToken = default);

    Task<InvoiceDto> CancelAsync(Guid invoiceId, CancellationToken cancellationToken = default);

    Task<InvoiceStatsDto> GetStatsAsync(CancellationToken cancellationToken = default);

    /// <summary>The branded invoice/receipt PDF the patient takes home.</summary>
    Task<(byte[] Content, string FileName)> GetPdfAsync(
        Guid invoiceId, CancellationToken cancellationToken = default);

    /// <summary>Outstanding dues grouped per patient, biggest debtor first.</summary>
    Task<DuesReportDto> GetDuesAsync(CancellationToken cancellationToken = default);

    /// <summary>Every invoice as CSV — for Excel, the accountant, or backup.</summary>
    Task<string> ExportCsvAsync(CancellationToken cancellationToken = default);
}
