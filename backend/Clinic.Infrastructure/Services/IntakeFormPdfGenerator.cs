using Clinic.Application.Features.Patients.DTOs;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Clinic.Infrastructure.Services;

/// <summary>
/// Printable patient intake form — modeled on the paper forms Kerala clinics
/// use today (clinic header, patient block, chief complaint, disease
/// checklist, histories, examination, findings table, informed consent).
/// The registration data is PRE-FILLED; clinical sections stay blank for the
/// doctor's pen. This is the paper→digital bridge clinics actually adopt.
///
/// Two seeded templates ship with every clinic:
///   "dental"  — oral health status, ortho findings, intra/extra oral exam
///   "general" — vitals strip, general + systemic examination
/// v3: full form builder (paid add-on) lets Admins design their own.
/// </summary>
public static class IntakeFormPdfGenerator
{
    static IntakeFormPdfGenerator()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    private const string Ink = "#0C2B23";
    private const string Teal = "#008465";
    private const string Border = "#B9CFC7";
    private const string Muted = "#5B6F68";
    private const string Bg = "#F4FAF7";

    private static readonly string[] DiseaseChecklist =
    [
        "Diabetic", "Blood Pressure", "Drug Allergies", "Latex Allergy",
        "Cardiac", "Anticoagulant Use", "Pregnancy", "Epilepsy",
        "Hepatic", "Renal", "Pulmonary", "STD",
    ];

    private static readonly string[] OralHealthLines =
        ["Calculus", "Stains", "Oral Hygiene", "Gingival health", "Periodontal health"];

    private static readonly string[] DentalExamLines =
        ["Face / Neck", "Lips / Cheeks", "Palate / Pharynx", "Tongue", "Floor / Frenum"];

    private static readonly string[] GeneralExamLines =
        ["Pallor / Icterus", "Cyanosis / Clubbing", "Lymph nodes", "Edema", "Built / Nutrition"];

    private static readonly string[] SystemicExamLines =
        ["CVS", "Respiratory", "Abdomen", "CNS", "Musculoskeletal"];

    private static readonly string[] VitalsFields =
        ["BP", "Pulse", "Temp", "SpO₂", "Weight", "Height"];

    /// <param name="template">"dental" (default) or "general" — which seeded layout to print.</param>
    public static byte[] Generate(
        string clinicName, string? clinicAddress, string? clinicPhone, PatientDto patient,
        string template = "dental")
    {
        var isDental = !string.Equals(template, "general", StringComparison.OrdinalIgnoreCase);

        return Document.Create(container =>
        {
            // ---------- Page 1: patient info + clinical intake ----------
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(36);
                page.DefaultTextStyle(t => t.FontSize(9.5f).FontColor(Ink));

                page.Header().Row(row =>
                {
                    row.RelativeItem().Column(col =>
                    {
                        col.Item().Text(clinicName).FontSize(19).SemiBold().FontColor(Ink);
                        if (!string.IsNullOrWhiteSpace(clinicAddress))
                            col.Item().Text(clinicAddress).FontSize(8.5f).FontColor(Muted);
                        if (!string.IsNullOrWhiteSpace(clinicPhone))
                            col.Item().Text($"☎ {clinicPhone}").FontSize(8.5f).FontColor(Muted);
                        col.Item().PaddingTop(2)
                            .Text(isDental ? "Dental intake form" : "General intake form")
                            .FontSize(8).SemiBold().FontColor(Teal);
                    });
                    row.ConstantItem(170).Column(col =>
                    {
                        col.Item().Border(1).BorderColor(Ink).Padding(6).Column(alert =>
                        {
                            alert.Item().Text("ALERTS").FontSize(7.5f).SemiBold().FontColor(Muted);
                            alert.Item().Height(18);
                        });
                        col.Item().PaddingTop(6).Text($"ID No: P-{patient.PatientNumber:D6}").SemiBold();
                        col.Item().Text($"Date: {DateTime.UtcNow:dd/MM/yyyy}");
                        col.Item().Text($"Sex: {patient.Gender}   Age: {(patient.Age.HasValue ? patient.Age.ToString() : "—")}");
                    });
                });

                page.Content().PaddingTop(10).Column(content =>
                {
                    // Patient info (pre-filled — the part reception hand-writes today)
                    content.Item().Background(Bg).Padding(10).Column(info =>
                    {
                        info.Item().Text("PATIENT INFORMATION").FontSize(8).SemiBold().FontColor(Teal);
                        info.Item().PaddingTop(4).Text($"Name of Patient:  {patient.FullName}").SemiBold();
                        info.Item().Text($"Address:  {patient.Address ?? "—"}");
                        info.Item().Row(r =>
                        {
                            r.RelativeItem().Text($"Mobile:  {patient.Phone}");
                            r.RelativeItem().Text($"E-mail:  {patient.Email ?? "—"}");
                        });
                    });

                    content.Item().PaddingTop(10).AlignCenter()
                        .Text("To be filled by the Doctor").FontSize(8.5f).SemiBold().FontColor(Muted);

                    // General template: a vitals strip is the first thing a
                    // physician records; dental clinics rarely chart these
                    if (!isDental)
                    {
                        content.Item().PaddingTop(8).Border(1).BorderColor(Border).Padding(8).Row(r =>
                        {
                            foreach (var vital in VitalsFields)
                            {
                                r.RelativeItem().Row(line =>
                                {
                                    line.AutoItem().Text($"{vital}: ").FontSize(8.5f).SemiBold();
                                    line.RelativeItem().PaddingTop(9).PaddingRight(6)
                                        .LineHorizontal(0.7f).LineColor(Muted);
                                });
                            }
                        });
                    }

                    // Chief complaint
                    LabeledBox(content, "Chief Complaint", 44);

                    // Disease checklist
                    content.Item().PaddingTop(8).Border(1).BorderColor(Border).Padding(8).Column(check =>
                    {
                        check.Item().Text("Medical Diseases Checklist").FontSize(8.5f).SemiBold();
                        check.Item().PaddingTop(4).Row(r =>
                        {
                            foreach (var chunk in DiseaseChecklist.Chunk(4))
                            {
                                r.RelativeItem().Column(c =>
                                {
                                    foreach (var disease in chunk)
                                        c.Item().PaddingVertical(1.5f).Row(line =>
                                        {
                                            line.ConstantItem(11).Height(9).Border(1).BorderColor(Ink);
                                            line.ConstantItem(5);
                                            line.RelativeItem().Text(disease).FontSize(8.5f);
                                        });
                                });
                            }
                        });
                    });

                    // Medical history | Medications
                    content.Item().PaddingTop(8).Row(r =>
                    {
                        r.RelativeItem(2).Border(1).BorderColor(Border).Padding(8).Column(c =>
                        {
                            c.Item().Text("Medical history").FontSize(8.5f).SemiBold();
                            c.Item().Height(52);
                            c.Item().Text(isDental ? "Dental history" : "Surgical / Family history")
                                .FontSize(8.5f).SemiBold();
                            c.Item().Height(52);
                        });
                        r.ConstantItem(8);
                        r.RelativeItem().Border(1).BorderColor(Border).Padding(8).Column(c =>
                        {
                            c.Item().Text("Medications").FontSize(8.5f).SemiBold();
                            c.Item().Height(118);
                        });
                    });

                    // Examination row — the section that differs per specialty
                    content.Item().PaddingTop(8).Row(r =>
                    {
                        if (isDental)
                        {
                            r.RelativeItem().Border(1).BorderColor(Border).Padding(8).Column(c =>
                            {
                                c.Item().Text("Ortho Findings (if any)").FontSize(8.5f).SemiBold();
                                c.Item().Height(96);
                            });
                            r.ConstantItem(8);
                            DottedSection(r.RelativeItem(), "Oral Health Status", OralHealthLines);
                            r.ConstantItem(8);
                            DottedSection(r.RelativeItem(), "Extra / Intra Oral Examination", DentalExamLines);
                        }
                        else
                        {
                            r.RelativeItem().Border(1).BorderColor(Border).Padding(8).Column(c =>
                            {
                                c.Item().Text("Local Examination").FontSize(8.5f).SemiBold();
                                c.Item().Height(96);
                            });
                            r.ConstantItem(8);
                            DottedSection(r.RelativeItem(), "General Examination", GeneralExamLines);
                            r.ConstantItem(8);
                            DottedSection(r.RelativeItem(), "Systemic Examination", SystemicExamLines);
                        }
                    });
                });

                page.Footer().AlignCenter()
                    .Text($"{clinicName} · Patient intake form · powered by Klivia")
                    .FontSize(7.5f).FontColor(Muted);
            });

            // ---------- Page 2: findings, treatment plan, consent ----------
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(36);
                page.DefaultTextStyle(t => t.FontSize(9.5f).FontColor(Ink));

                page.Content().Column(content =>
                {
                    LabeledBox(content, "Investigations", 70);
                    LabeledBox(content, isDental
                        ? "Previous Dental Treatment (if any, please specify)"
                        : "Previous Treatment / Hospitalisation (if any, please specify)", 56);

                    // Findings / treatment table
                    content.Item().PaddingTop(10).Table(table =>
                    {
                        table.ColumnsDefinition(cols =>
                        {
                            cols.ConstantColumn(30);
                            cols.RelativeColumn(3);
                            cols.RelativeColumn(3);
                            cols.ConstantColumn(70);
                        });
                        table.Header(h =>
                        {
                            foreach (var title in new[]
                                { "Sl. No", "Clinical / Radiographic Findings", "Respective Treatment Advised", "Selected Tx" })
                                h.Cell().Border(1).BorderColor(Ink).Padding(5)
                                    .Text(title).FontSize(8.5f).SemiBold();
                        });
                        for (var i = 0; i < 6; i++)
                            for (var col = 0; col < 4; col++)
                                table.Cell().Border(0.8f).BorderColor(Border).Height(30);
                    });

                    content.Item().PaddingTop(8).Text("Initial Estimated Cost of Selected Tx:  ₹ __________________")
                        .FontSize(9.5f).SemiBold();

                    // Informed consent — Malayalam version arrives with i18n
                    content.Item().PaddingTop(16).AlignCenter()
                        .Text("PATIENT TREATMENT INFORMED CONSENT").FontSize(10.5f).SemiBold();
                    content.Item().PaddingTop(6).Text(
                        "I have been fully informed of the nature of the procedures involved in the treatment " +
                        $"of my {(isDental ? "dental" : "medical")} conditions, the procedures to be utilized, the risks and benefits of the " +
                        "treatment, the anesthesia selected, and the necessity of follow-up and self-care. The " +
                        "treatments have been decided in consultation with me after analysing the risks, " +
                        "benefits and the costs involved.").FontSize(8.5f).LineHeight(1.5f);
                    content.Item().PaddingTop(5).Text(
                        "I have had the opportunity to ask any questions I may have in connection with the " +
                        "treatment and to discuss my concerns with the Doctor. I hereby consent to the " +
                        "performance of the procedures as presented to me during consultation and the " +
                        "treatment plan described in this document.").FontSize(8.5f).LineHeight(1.5f);
                    content.Item().PaddingTop(5).Text(
                        "I CERTIFY THAT I HAVE READ AND FULLY UNDERSTAND THIS CONSENT DOCUMENT.")
                        .FontSize(8.5f).SemiBold();

                    content.Item().PaddingTop(22).Row(r =>
                    {
                        r.RelativeItem().Column(c =>
                        {
                            c.Item().Text("Name of Patient / Guardian:  ______________________").FontSize(9);
                            c.Item().PaddingTop(12).Text("Signature:  ______________________").FontSize(9);
                        });
                        r.ConstantItem(140).AlignRight().Text($"Date: {DateTime.UtcNow:dd/MM/yyyy}").FontSize(9);
                    });
                });

                page.Footer().AlignCenter()
                    .Text($"{clinicName} · Page 2 of 2").FontSize(7.5f).FontColor(Muted);
            });
        }).GeneratePdf();
    }

    private static void LabeledBox(ColumnDescriptor content, string label, float height)
    {
        content.Item().PaddingTop(8).Border(1).BorderColor(Border).Padding(8).Column(c =>
        {
            c.Item().Text(label).FontSize(8.5f).SemiBold();
            c.Item().Height(height);
        });
    }

    private static void DottedSection(IContainer container, string title, string[] lines)
    {
        container.Border(1).BorderColor(Border).Padding(8).Column(c =>
        {
            c.Item().Text(title).FontSize(8.5f).SemiBold();
            foreach (var line in lines)
                c.Item().PaddingTop(6).Row(r =>
                {
                    r.ConstantItem(78).Text(line).FontSize(8.5f);
                    r.RelativeItem().PaddingTop(9).LineHorizontal(0.7f).LineColor(Muted);
                });
        });
    }
}
