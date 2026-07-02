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
    private const string Border = "#E2EDE8";
    private const string Muted = "#5B6F68";

    public static byte[] Generate(PrescriptionPdfData data)
    {
        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(40);
                page.DefaultTextStyle(t => t.FontSize(10).FontColor(Ink));

                page.Header().Column(header =>
                {
                    header.Item().Row(row =>
                    {
                        row.RelativeItem().Column(col =>
                        {
                            col.Item().Text(data.ClinicName)
                                .FontSize(20).SemiBold().FontColor(Ink);
                            col.Item().Text("Prescription")
                                .FontSize(11).FontColor(Teal).SemiBold();
                        });
                        row.ConstantItem(160).AlignRight().Column(col =>
                        {
                            col.Item().Text($"Date: {data.IssuedAt:dd MMM yyyy}").FontColor(Muted);
                            col.Item().Text($"Doctor: {data.DoctorName}").FontColor(Muted);
                        });
                    });
                    header.Item().PaddingTop(12).LineHorizontal(2).LineColor(Teal);
                });

                page.Content().PaddingVertical(16).Column(content =>
                {
                    // Patient block
                    content.Item().Background("#F4FAF7").Padding(12).Row(row =>
                    {
                        row.RelativeItem().Column(col =>
                        {
                            col.Item().Text("PATIENT").FontSize(8).FontColor(Muted).SemiBold();
                            col.Item().Text(data.PatientName).FontSize(12).SemiBold();
                        });
                        row.RelativeItem().Column(col =>
                        {
                            col.Item().Text("PHONE").FontSize(8).FontColor(Muted).SemiBold();
                            col.Item().Text(data.PatientPhone);
                        });
                        if (data.PatientDateOfBirth.HasValue)
                        {
                            row.RelativeItem().Column(col =>
                            {
                                col.Item().Text("DATE OF BIRTH").FontSize(8).FontColor(Muted).SemiBold();
                                col.Item().Text($"{data.PatientDateOfBirth:dd MMM yyyy}");
                            });
                        }
                    });

                    // Diagnosis
                    content.Item().PaddingTop(16).Text("Diagnosis")
                        .FontSize(11).SemiBold().FontColor(Teal);
                    content.Item().PaddingTop(4).Text(data.Diagnosis);

                    // Medicines table
                    content.Item().PaddingTop(16).Text("Medicines")
                        .FontSize(11).SemiBold().FontColor(Teal);

                    content.Item().PaddingTop(6).Table(table =>
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
                            foreach (var title in new[] { "#", "Medicine", "Dosage", "Frequency", "Duration", "Instructions" })
                                h.Cell().BorderBottom(1).BorderColor(Ink).PaddingVertical(4)
                                    .Text(title).FontSize(8).SemiBold().FontColor(Muted);
                        });

                        var index = 1;
                        foreach (var item in data.Items)
                        {
                            table.Cell().BorderBottom(1).BorderColor(Border).PaddingVertical(6)
                                .Text(index++.ToString()).FontColor(Muted);
                            table.Cell().BorderBottom(1).BorderColor(Border).PaddingVertical(6)
                                .Text(item.MedicineName).SemiBold();
                            table.Cell().BorderBottom(1).BorderColor(Border).PaddingVertical(6)
                                .Text(item.Dosage ?? "—");
                            table.Cell().BorderBottom(1).BorderColor(Border).PaddingVertical(6)
                                .Text(item.Frequency ?? "—");
                            table.Cell().BorderBottom(1).BorderColor(Border).PaddingVertical(6)
                                .Text(item.DurationDays.HasValue ? $"{item.DurationDays} days" : "—");
                            table.Cell().BorderBottom(1).BorderColor(Border).PaddingVertical(6)
                                .Text(item.Instructions ?? "—");
                        }
                    });

                    if (!string.IsNullOrWhiteSpace(data.Notes))
                    {
                        content.Item().PaddingTop(16).Text("Notes")
                            .FontSize(11).SemiBold().FontColor(Teal);
                        content.Item().PaddingTop(4).Text(data.Notes!);
                    }

                    // Signature
                    content.Item().PaddingTop(48).AlignRight().Column(col =>
                    {
                        col.Item().Width(180).LineHorizontal(1).LineColor(Ink);
                        col.Item().PaddingTop(4).AlignRight()
                            .Text($"{data.DoctorName}").SemiBold();
                        col.Item().AlignRight()
                            .Text("Signature").FontSize(8).FontColor(Muted);
                    });
                });

                page.Footer().AlignCenter()
                    .Text($"{data.ClinicName} — generated {DateTime.UtcNow:dd MMM yyyy HH:mm} UTC")
                    .FontSize(8).FontColor(Muted);
            });
        }).GeneratePdf();
    }
}
