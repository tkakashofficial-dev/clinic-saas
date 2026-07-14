using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Clinic.Infrastructure.Services;

public class PrescriptionPdfData
{
    public string ClinicName { get; set; } = default!;
    public string PatientName { get; set; } = default!;
    public string PatientPhone { get; set; } = default!;
    public DateOnly? PatientDateOfBirth { get; set; }
    public string DoctorName { get; set; } = default!;
    public string Diagnosis { get; set; } = default!;
    public string? Notes { get; set; }
    public DateTime IssuedAt { get; set; }
    public List<PrescriptionPdfItem> Items { get; set; } = new();
}

public class PrescriptionPdfItem
{
    public string MedicineName { get; set; } = default!;
    public string? Dosage { get; set; }
    public string? Frequency { get; set; }
    public int? DurationDays { get; set; }
    public string? Instructions { get; set; }
}

/// <summary>
/// Renders prescriptions with QuestPDF using the product's design tokens
/// (see docs/design/design-system.md) so the printout matches the brand.
/// A prescription is the artifact patients carry to the pharmacy and keep at
/// home — it IS the clinic's business card, so it must look premium.
/// </summary>
public static class PrescriptionPdfGenerator
{
    // Static ctor: the license is configured wherever this class is used
    // (app, tests, future tools) — not only when DI happens to run.
    static PrescriptionPdfGenerator()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    // Brand tokens from the design system
    private const string Ink = "#0C2B23";
    private const string Teal = "#008465";
    private const string TealSoft = "#E7F5F0";
    private const string Border = "#E2EDE8";
    private const string Muted = "#5B6F68";
    private const string Zebra = "#F7FBF9";

    public static byte[] Generate(PrescriptionPdfData data)
    {
        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(0);
                page.DefaultTextStyle(t => t.FontSize(10).FontColor(Ink));

                // Full-bleed ink header band — the "premium letterhead" moment
                page.Header().Background(Ink).PaddingHorizontal(40).PaddingVertical(22).Row(row =>
                {
                    row.RelativeItem().Column(col =>
                    {
                        col.Item().Row(brand =>
                        {
                            brand.AutoItem().Width(22).Height(22).Background(Teal)
                                .AlignCenter().AlignMiddle()
                                .Text("+").FontSize(15).SemiBold().FontColor(Colors.White);
                            brand.AutoItem().PaddingLeft(8).AlignMiddle()
                                .Text(data.ClinicName).FontSize(18).SemiBold().FontColor(Colors.White);
                        });
                        col.Item().PaddingTop(4).Text("MEDICAL PRESCRIPTION")
                            .FontSize(8).SemiBold().FontColor("#7FC8B4").LetterSpacing(0.2f);
                    });
                    row.ConstantItem(170).AlignRight().AlignMiddle().Column(col =>
                    {
                        col.Item().AlignRight().Text(data.DoctorName)
                            .FontSize(11).SemiBold().FontColor(Colors.White);
                        col.Item().AlignRight().Text($"{data.IssuedAt:dd MMM yyyy}")
                            .FontSize(9).FontColor("#9DBAB1");
                    });
                });

                page.Content().PaddingHorizontal(40).PaddingVertical(20).Column(content =>
                {
                    // Patient chips row
                    content.Item().Row(row =>
                    {
                        void Chip(RowDescriptor r, string label, string value, bool wide = false)
                        {
                            var item = wide ? r.RelativeItem(2) : r.RelativeItem();
                            item.PaddingRight(8).Element(e => e
                                .Background(TealSoft).Padding(10).Column(col =>
                                {
                                    col.Item().Text(label).FontSize(7.5f).SemiBold().FontColor(Teal);
                                    col.Item().PaddingTop(2).Text(value).FontSize(10.5f).SemiBold();
                                }));
                        }

                        Chip(row, "PATIENT", data.PatientName, wide: true);
                        Chip(row, "PHONE", data.PatientPhone);
                        Chip(row, "DATE OF BIRTH",
                            data.PatientDateOfBirth.HasValue
                                ? $"{data.PatientDateOfBirth:dd MMM yyyy}" : "—");
                    });

                    // ℞ + diagnosis
                    content.Item().PaddingTop(18).Row(row =>
                    {
                        row.ConstantItem(34).Text("℞").FontSize(26).SemiBold().FontColor(Teal);
                        row.RelativeItem().PaddingTop(4).Column(col =>
                        {
                            col.Item().Text("DIAGNOSIS").FontSize(7.5f).SemiBold().FontColor(Muted);
                            col.Item().PaddingTop(2).Text(data.Diagnosis).FontSize(11.5f).SemiBold();
                        });
                    });

                    // Medicines table — teal header band, zebra rows
                    content.Item().PaddingTop(14).Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.ConstantColumn(24);   // #
                            columns.RelativeColumn(3);    // Medicine
                            columns.RelativeColumn(2);    // Dosage
                            columns.RelativeColumn(2);    // Frequency
                            columns.RelativeColumn(1.4f); // Duration
                            columns.RelativeColumn(3);    // Instructions
                        });

                        table.Header(h =>
                        {
                            foreach (var title in new[] { "#", "MEDICINE", "DOSAGE", "FREQUENCY", "DURATION", "INSTRUCTIONS" })
                                h.Cell().Background(Teal).PaddingVertical(7).PaddingHorizontal(6)
                                    .Text(title).FontSize(7.5f).SemiBold().FontColor(Colors.White);
                        });

                        var index = 0;
                        foreach (var item in data.Items)
                        {
                            var bg = index % 2 == 1 ? Zebra : "#FFFFFF";
                            index++;

                            IContainer Cell() => table.Cell().Background(bg)
                                .BorderBottom(1).BorderColor(Border)
                                .PaddingVertical(8).PaddingHorizontal(6);

                            Cell().Text(index.ToString()).FontColor(Muted);
                            Cell().Text(item.MedicineName).SemiBold();
                            Cell().Text(item.Dosage ?? "—");
                            Cell().Text(item.Frequency ?? "—");
                            Cell().Text(item.DurationDays.HasValue ? $"{item.DurationDays} days" : "—");
                            Cell().Text(item.Instructions ?? "—").FontSize(9).FontColor(Muted);
                        }
                    });

                    if (!string.IsNullOrWhiteSpace(data.Notes))
                    {
                        content.Item().PaddingTop(14)
                            .BorderLeft(3).BorderColor(Teal).Background(TealSoft)
                            .Padding(10).Column(col =>
                            {
                                col.Item().Text("DOCTOR'S NOTES").FontSize(7.5f).SemiBold().FontColor(Teal);
                                col.Item().PaddingTop(3).Text(data.Notes!).FontSize(9.5f).LineHeight(1.4f);
                            });
                    }

                    // Signature
                    content.Item().PaddingTop(44).AlignRight().Column(col =>
                    {
                        col.Item().Width(180).LineHorizontal(1).LineColor(Ink);
                        col.Item().PaddingTop(4).AlignRight()
                            .Text(data.DoctorName).SemiBold();
                        col.Item().AlignRight()
                            .Text("Signature & Stamp").FontSize(8).FontColor(Muted);
                    });
                });

                // Footer band mirrors the header — closes the frame
                page.Footer().Background(Ink).PaddingHorizontal(40).PaddingVertical(10).Row(row =>
                {
                    row.RelativeItem().Text(data.ClinicName)
                        .FontSize(8).SemiBold().FontColor(Colors.White);
                    row.RelativeItem().AlignRight()
                        .Text($"Generated {DateTime.UtcNow:dd MMM yyyy} · powered by Klivia")
                        .FontSize(8).FontColor("#9DBAB1");
                });
            });
        }).GeneratePdf();
    }
}
