using Clinic.Domain.Constants;
using Clinic.Domain.Entities;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Clinic.Infrastructure.Services;

/// <summary>
/// The patient's bill — printed or WhatsApp'd home. Like the prescription,
/// it doubles as the clinic's business card, so it uses the same letterhead
/// language: ink bands, teal accents, clean numbers.
/// </summary>
public static class InvoicePdfGenerator
{
    static InvoicePdfGenerator()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    private const string Ink = "#0C2B23";
    private const string Teal = "#008465";
    private const string TealBright = "#00BD8F";
    private const string TealSoft = "#E7F5F0";
    private const string Border = "#E2EDE8";
    private const string Muted = "#5B6F68";
    private const string Zebra = "#F7FBF9";

    public static byte[] Generate(
        string clinicName, string? clinicAddress, string? clinicPhone, Invoice invoice)
    {
        var isPaid = invoice.Status == InvoiceStatuses.Paid;
        var isCancelled = invoice.Status == InvoiceStatuses.Cancelled;

        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(0);
                page.DefaultTextStyle(t => t.FontSize(10).FontColor(Ink));

                // Letterhead band
                page.Header().Background(Ink).PaddingHorizontal(40).PaddingVertical(22).Row(row =>
                {
                    row.RelativeItem().Column(col =>
                    {
                        col.Item().Row(brand =>
                        {
                            brand.AutoItem().Width(22).Height(22).Background(TealBright)
                                .AlignCenter().AlignMiddle()
                                .Text("+").FontSize(15).SemiBold().FontColor("#06362B");
                            brand.AutoItem().PaddingLeft(8).AlignMiddle()
                                .Text(clinicName).FontSize(18).SemiBold().FontColor(Colors.White);
                        });
                        var contact = string.Join("  ·  ",
                            new[] { clinicAddress, clinicPhone }.Where(v => !string.IsNullOrWhiteSpace(v))!);
                        if (contact.Length > 0)
                            col.Item().PaddingTop(3).Text(contact).FontSize(8).FontColor("#9DBAB1");
                        col.Item().PaddingTop(3).Text(isPaid ? "RECEIPT" : "INVOICE")
                            .FontSize(8).SemiBold().FontColor("#7FC8B4").LetterSpacing(0.2f);
                    });
                    row.ConstantItem(170).AlignRight().AlignMiddle().Column(col =>
                    {
                        col.Item().AlignRight().Text($"INV-{invoice.InvoiceNumber:D6}")
                            .FontSize(13).SemiBold().FontColor(Colors.White);
                        col.Item().AlignRight().Text($"{invoice.CreatedAt:dd MMM yyyy}")
                            .FontSize(9).FontColor("#9DBAB1");
                    });
                });

                page.Content().PaddingHorizontal(40).PaddingVertical(20).Column(content =>
                {
                    // Bill-to + status
                    content.Item().Row(row =>
                    {
                        row.RelativeItem(2).PaddingRight(8).Element(e => e
                            .Background(TealSoft).Padding(12).Column(col =>
                            {
                                col.Item().Text("BILLED TO").FontSize(7.5f).SemiBold().FontColor(Teal);
                                col.Item().PaddingTop(2)
                                    .Text($"{invoice.Patient.FirstName} {invoice.Patient.LastName}")
                                    .FontSize(12).SemiBold();
                                col.Item().Text(invoice.Patient.Phone).FontSize(9.5f).FontColor(Muted);
                            }));
                        row.RelativeItem().Element(e => e
                            .Background(isPaid ? TealSoft : isCancelled ? "#F1F5F4" : "#FEF7E5")
                            .Padding(12).Column(col =>
                            {
                                col.Item().Text("STATUS").FontSize(7.5f).SemiBold()
                                    .FontColor(isPaid ? Teal : isCancelled ? Muted : "#8A6D1B");
                                col.Item().PaddingTop(2).Text(
                                        isPaid ? "PAID ✓" : isCancelled ? "CANCELLED" : "UNPAID")
                                    .FontSize(13).SemiBold()
                                    .FontColor(isPaid ? Teal : isCancelled ? Muted : "#B45309");
                                if (isPaid)
                                    col.Item().Text($"{MethodLabel(invoice.PaymentMethod)} · {invoice.PaidAt:dd MMM yyyy}")
                                        .FontSize(8.5f).FontColor(Muted);
                            }));
                    });

                    // Items table
                    content.Item().PaddingTop(18).Table(table =>
                    {
                        table.ColumnsDefinition(cols =>
                        {
                            cols.ConstantColumn(26);   // #
                            cols.RelativeColumn(5);    // description
                            cols.ConstantColumn(46);   // qty
                            cols.ConstantColumn(80);   // rate
                            cols.ConstantColumn(90);   // amount
                        });

                        table.Header(h =>
                        {
                            foreach (var (title, right) in new[]
                                { ("#", false), ("DESCRIPTION", false), ("QTY", true), ("RATE ₹", true), ("AMOUNT ₹", true) })
                            {
                                var cell = h.Cell().Background(Teal).PaddingVertical(7).PaddingHorizontal(6);
                                var text = right ? cell.AlignRight() : cell;
                                text.Text(title).FontSize(7.5f).SemiBold().FontColor(Colors.White);
                            }
                        });

                        var index = 0;
                        foreach (var item in invoice.Items)
                        {
                            var bg = index % 2 == 1 ? Zebra : "#FFFFFF";
                            index++;

                            IContainer Cell(bool right = false)
                            {
                                var c = table.Cell().Background(bg)
                                    .BorderBottom(1).BorderColor(Border)
                                    .PaddingVertical(8).PaddingHorizontal(6);
                                return right ? c.AlignRight() : c;
                            }

                            Cell().Text(index.ToString()).FontColor(Muted);
                            Cell().Text(item.Description).SemiBold();
                            Cell(right: true).Text(item.Quantity.ToString());
                            Cell(right: true).Text($"{item.UnitPriceRupees:N2}");
                            Cell(right: true).Text($"{item.LineTotalRupees:N2}").SemiBold();
                        }
                    });

                    // Totals block, right-aligned
                    content.Item().PaddingTop(12).AlignRight().Width(240).Column(totals =>
                    {
                        void Line(string label, string value, bool strong = false)
                        {
                            totals.Item().PaddingVertical(3).Row(r =>
                            {
                                r.RelativeItem().Text(label).FontSize(strong ? 11 : 9.5f)
                                    .FontColor(strong ? Ink : Muted).SemiBold();
                                r.AutoItem().Text(value).FontSize(strong ? 13 : 9.5f).SemiBold();
                            });
                        }

                        Line("Subtotal", $"₹ {invoice.SubtotalRupees:N2}");
                        if (invoice.DiscountRupees > 0)
                            Line("Discount", $"− ₹ {invoice.DiscountRupees:N2}");
                        totals.Item().PaddingVertical(4).LineHorizontal(1).LineColor(Ink);
                        Line("Total", $"₹ {invoice.TotalRupees:N2}", strong: true);
                    });

                    if (!string.IsNullOrWhiteSpace(invoice.Notes))
                    {
                        content.Item().PaddingTop(14)
                            .BorderLeft(3).BorderColor(TealBright).Background(TealSoft)
                            .Padding(10).Column(col =>
                            {
                                col.Item().Text("NOTES").FontSize(7.5f).SemiBold().FontColor(Teal);
                                col.Item().PaddingTop(2).Text(invoice.Notes!).FontSize(9.5f);
                            });
                    }

                    content.Item().PaddingTop(26).Text(
                            isPaid
                                ? "Thank you — wishing you a speedy recovery! 💚"
                                : "Please settle this invoice at the reception desk.")
                        .FontSize(9.5f).FontColor(Muted);
                });

                page.Footer().Background(Ink).PaddingHorizontal(40).PaddingVertical(10).Row(row =>
                {
                    row.RelativeItem().Text(clinicName)
                        .FontSize(8).SemiBold().FontColor(Colors.White);
                    row.RelativeItem().AlignRight()
                        .Text($"Generated {DateTime.UtcNow:dd MMM yyyy} · powered by Klivia")
                        .FontSize(8).FontColor("#9DBAB1");
                });
            });
        }).GeneratePdf();
    }

    private static string MethodLabel(string? method) => method switch
    {
        PaymentMethods.Upi => "UPI",
        PaymentMethods.BankTransfer => "Bank transfer",
        _ => method ?? "—",
    };
}
