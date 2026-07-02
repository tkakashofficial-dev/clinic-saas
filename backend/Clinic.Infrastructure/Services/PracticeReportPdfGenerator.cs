using Clinic.Application.Features.Reports.DTOs;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Clinic.Infrastructure.Services;

/// <summary>
/// Renders the practice overview as a branded PDF — owners print this or
/// send it to partners/accountants.
/// </summary>
public static class PracticeReportPdfGenerator
{
    static PracticeReportPdfGenerator()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    private const string Ink = "#0C2B23";
    private const string Teal = "#008465";
    private const string TealBright = "#00BD8F";
    private const string Border = "#E2EDE8";
    private const string Muted = "#5B6F68";
    private const string Bg = "#F4FAF7";

    public static byte[] Generate(string clinicName, PracticeOverviewDto data)
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
                            col.Item().Text(clinicName).FontSize(20).SemiBold().FontColor(Ink);
                            col.Item().Text("Practice report · last 30 days")
                                .FontSize(11).FontColor(Teal).SemiBold();
                        });
                        row.ConstantItem(150).AlignRight()
                            .Text($"Generated {DateTime.UtcNow:d MMM yyyy}").FontColor(Muted);
                    });
                    header.Item().PaddingTop(12).LineHorizontal(2).LineColor(Teal);
                });

                page.Content().PaddingVertical(16).Column(content =>
                {
                    // Stat tiles
                    content.Item().Row(row =>
                    {
                        AddStat(row, "Patients", data.TotalPatients.ToString());
                        AddStat(row, "New (30d)", data.NewPatientsLast30Days.ToString());
                        AddStat(row, "Today", data.AppointmentsToday.ToString());
                        AddStat(row, "Completed", data.CompletedLast30Days.ToString());
                        AddStat(row, "Cancelled", data.CancelledLast30Days.ToString());
                    });

                    // Appointments per day — horizontal bars
                    content.Item().PaddingTop(20).Text("Appointments — last 14 days")
                        .FontSize(11).SemiBold().FontColor(Teal);

                    var maxPerDay = Math.Max(1, data.AppointmentsPerDay.Max(d => d.Count));
                    content.Item().PaddingTop(6).Column(chart =>
                    {
                        foreach (var day in data.AppointmentsPerDay)
                        {
                            chart.Item().Row(row =>
                            {
                                row.ConstantItem(52).Text($"{day.Date:dd MMM}")
                                    .FontSize(8).FontColor(Muted);
                                row.RelativeItem().PaddingVertical(1.5f).Row(barRow =>
                                {
                                    var portion = (float)day.Count / maxPerDay;
                                    if (day.Count > 0)
                                    {
                                        barRow.RelativeItem(Math.Max(portion, 0.02f))
                                            .Height(9).Background(TealBright);
                                        if (portion < 1)
                                            barRow.RelativeItem(1 - portion);
                                    }
                                    else
                                    {
                                        barRow.RelativeItem().Height(9).Background(Bg);
                                    }
                                });
                                row.ConstantItem(22).AlignRight()
                                    .Text(day.Count.ToString()).FontSize(8).SemiBold();
                            });
                        }
                    });

                    // Status mix
                    content.Item().PaddingTop(20).Text("Status mix — last 30 days")
                        .FontSize(11).SemiBold().FontColor(Teal);
                    content.Item().PaddingTop(6).Row(row =>
                    {
                        foreach (var status in data.ByStatusLast30Days)
                        {
                            row.RelativeItem().Background(Bg).Padding(8).Column(col =>
                            {
                                col.Item().Text(status.Status).FontSize(8).FontColor(Muted);
                                col.Item().Text(status.Count.ToString()).FontSize(14).SemiBold();
                            });
                            row.ConstantItem(6);
                        }
                    });

                    // Per-doctor table
                    content.Item().PaddingTop(20).Text("Doctor performance — last 30 days")
                        .FontSize(11).SemiBold().FontColor(Teal);
                    content.Item().PaddingTop(6).Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.RelativeColumn(3);
                            columns.RelativeColumn(1.2f);
                            columns.RelativeColumn(1.2f);
                            columns.RelativeColumn(1.4f);
                        });

                        table.Header(h =>
                        {
                            foreach (var title in new[] { "Doctor", "Appointments", "Completed", "Completion" })
                                h.Cell().BorderBottom(1).BorderColor(Ink).PaddingVertical(4)
                                    .Text(title).FontSize(8).SemiBold().FontColor(Muted);
                        });

                        foreach (var doctor in data.PerDoctorLast30Days)
                        {
                            var rate = doctor.Total == 0
                                ? 0
                                : (int)Math.Round(100.0 * doctor.Completed / doctor.Total);
                            table.Cell().BorderBottom(1).BorderColor(Border).PaddingVertical(6)
                                .Text(doctor.DoctorName).SemiBold();
                            table.Cell().BorderBottom(1).BorderColor(Border).PaddingVertical(6)
                                .Text(doctor.Total.ToString());
                            table.Cell().BorderBottom(1).BorderColor(Border).PaddingVertical(6)
                                .Text(doctor.Completed.ToString());
                            table.Cell().BorderBottom(1).BorderColor(Border).PaddingVertical(6)
                                .Text($"{rate}%").FontColor(Teal).SemiBold();
                        }
                    });
                });

                page.Footer().AlignCenter()
                    .Text($"{clinicName} — powered by Klivia")
                    .FontSize(8).FontColor(Muted);
            });
        }).GeneratePdf();
    }

    private static void AddStat(RowDescriptor row, string label, string value)
    {
        row.RelativeItem().Background(Bg).Padding(10).Column(col =>
        {
            col.Item().Text(label).FontSize(8).SemiBold().FontColor(Muted);
            col.Item().Text(value).FontSize(16).SemiBold();
        });
        row.ConstantItem(6);
    }
}
