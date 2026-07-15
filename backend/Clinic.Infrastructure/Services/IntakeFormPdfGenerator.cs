using Clinic.Application.Features.Forms.DTOs;
using Clinic.Application.Features.Patients.DTOs;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Clinic.Infrastructure.Services;

/// <summary>
/// Printable patient intake form — modeled on the paper forms Kerala clinics
/// use today, styled like the clinic's letterhead (ink header band, teal
/// accents) so the printout looks premium while staying pen-friendly.
/// Registration data is PRE-FILLED; clinical sections stay blank.
///
/// Two seeded templates ("dental" | "general") + the clinic's own CUSTOM
/// sections (form builder v1) which print on additional pages.
/// </summary>
public static class IntakeFormPdfGenerator
{
    static IntakeFormPdfGenerator()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    private const string Ink = "#0C2B23";
    private const string Teal = "#008465";
    private const string TealBright = "#00BD8F";
    private const string TealSoft = "#E7F5F0";
    private const string Border = "#D6E7E0";
    private const string Muted = "#5B6F68";
    private const string Mint = "#F4FAF7";
    private const string HeaderSub = "#9DBAB1";

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

    /// <param name="template">"dental" (default) or "general".</param>
    /// <param name="customSections">The clinic's own form-builder sections (may be null).</param>
    /// <param name="answers">Digital answers captured by staff — prints PRE-FILLED (may be null).</param>
    public static byte[] Generate(
        string clinicName, string? clinicAddress, string? clinicPhone, PatientDto patient,
        string template = "dental", List<IntakeFormSectionDto>? customSections = null,
        IntakeAnswersDto? answers = null)
    {
        var isDental = !string.Equals(template, "general", StringComparison.OrdinalIgnoreCase);
        var sections = customSections ?? [];
        var totalPages = 2 + (sections.Count > 0 ? ChunkSections(sections).Count : 0);

        return Document.Create(container =>
        {
            // ---------- Page 1: patient info + clinical intake ----------
            container.Page(page =>
            {
                ApplyChrome(page, clinicName, clinicAddress, clinicPhone,
                    isDental, patient, pageNumber: 1, totalPages);

                page.Content().PaddingHorizontal(34).PaddingVertical(10).Column(content =>
                {
                    // Alerts: the one thing a doctor must never miss — amber
                    content.Item().Background("#FEF7E5").BorderLeft(3).BorderColor("#F0B429")
                        .Padding(8).Row(r =>
                        {
                            r.AutoItem().Text("⚠ ALERTS").FontSize(8).SemiBold().FontColor("#8A6D1B");
                            r.RelativeItem().PaddingLeft(10).PaddingTop(6)
                                .LineHorizontal(0.8f).LineColor("#E4CE96");
                        });

                    // Patient info (pre-filled — what reception hand-writes today)
                    SectionTitle(content, "Patient information");
                    content.Item().Background(Mint).Border(1).BorderColor(Border)
                        .Padding(10).Column(info =>
                    {
                        info.Item().Row(r =>
                        {
                            r.RelativeItem(2).Text(t =>
                            {
                                t.Span("Name:  ").FontSize(8.5f).FontColor(Muted);
                                t.Span(patient.FullName).FontSize(10).SemiBold();
                            });
                            r.RelativeItem().Text(t =>
                            {
                                t.Span("Mobile:  ").FontSize(8.5f).FontColor(Muted);
                                t.Span(patient.Phone).FontSize(9.5f).SemiBold();
                            });
                        });
                        info.Item().PaddingTop(4).Row(r =>
                        {
                            r.RelativeItem(2).Text(t =>
                            {
                                t.Span("Address:  ").FontSize(8.5f).FontColor(Muted);
                                t.Span(patient.Address ?? "—").FontSize(9);
                            });
                            r.RelativeItem().Text(t =>
                            {
                                t.Span("E-mail:  ").FontSize(8.5f).FontColor(Muted);
                                t.Span(patient.Email ?? "—").FontSize(9);
                            });
                        });
                    });

                    content.Item().PaddingTop(8).AlignCenter()
                        .Text(answers is null
                            ? "· To be filled by the doctor ·"
                            : "· Recorded digitally with the patient — please verify ·")
                        .FontSize(8).SemiBold().FontColor(Muted);

                    // General template: vitals strip first, as physicians chart
                    if (!isDental)
                    {
                        content.Item().PaddingTop(6).Border(1).BorderColor(Border).Padding(8).Row(r =>
                        {
                            foreach (var vital in VitalsFields)
                            {
                                r.RelativeItem().Row(line =>
                                {
                                    line.AutoItem().Text($"{vital}: ").FontSize(8.5f).SemiBold();
                                    line.RelativeItem().PaddingTop(9).PaddingRight(6)
                                        .LineHorizontal(0.8f).LineColor(Border);
                                });
                            }
                        });
                    }

                    SectionTitle(content, "Chief complaint");
                    WritingBox(content, 34, answers?.ChiefComplaint);

                    SectionTitle(content, "Medical diseases checklist");
                    content.Item().Border(1).BorderColor(Border).Padding(9).Row(r =>
                    {
                        foreach (var chunk in DiseaseChecklist.Chunk(4))
                        {
                            r.RelativeItem().Column(c =>
                            {
                                foreach (var disease in chunk)
                                    CheckItem(c, disease,
                                        answers?.DiseaseChecklist.Contains(disease) == true);
                            });
                        }
                    });

                    SectionTitle(content, isDental
                        ? "Medical & dental history  ·  Medications"
                        : "Medical, surgical & family history  ·  Medications");
                    content.Item().Row(r =>
                    {
                        r.RelativeItem(2).Border(1).BorderColor(Border).Padding(8).Column(c =>
                        {
                            c.Item().Text("Medical history").FontSize(8.5f).SemiBold().FontColor(Teal);
                            FilledOrBlank(c, 40, answers?.MedicalHistory);
                            c.Item().Text(isDental ? "Dental history" : "Surgical / family history")
                                .FontSize(8.5f).SemiBold().FontColor(Teal);
                            FilledOrBlank(c, 40, answers?.SecondaryHistory);
                        });
                        r.ConstantItem(8);
                        r.RelativeItem().Border(1).BorderColor(Border).Padding(8).Column(c =>
                        {
                            c.Item().Text("Medications").FontSize(8.5f).SemiBold().FontColor(Teal);
                            FilledOrBlank(c, 94, answers?.Medications);
                        });
                    });

                    SectionTitle(content, "Examination");
                    content.Item().Row(r =>
                    {
                        if (isDental)
                        {
                            r.RelativeItem().Border(1).BorderColor(Border).Padding(8).Column(c =>
                            {
                                c.Item().Text("Ortho findings (if any)").FontSize(8.5f).SemiBold().FontColor(Teal);
                                c.Item().Height(76);
                            });
                            r.ConstantItem(8);
                            DottedSection(r.RelativeItem(), "Oral health status", OralHealthLines);
                            r.ConstantItem(8);
                            DottedSection(r.RelativeItem(), "Extra / intra oral", DentalExamLines);
                        }
                        else
                        {
                            r.RelativeItem().Border(1).BorderColor(Border).Padding(8).Column(c =>
                            {
                                c.Item().Text("Local examination").FontSize(8.5f).SemiBold().FontColor(Teal);
                                c.Item().Height(76);
                            });
                            r.ConstantItem(8);
                            DottedSection(r.RelativeItem(), "General examination", GeneralExamLines);
                            r.ConstantItem(8);
                            DottedSection(r.RelativeItem(), "Systemic examination", SystemicExamLines);
                        }
                    });
                });
            });

            // ---------- Page 2: findings, treatment plan, consent ----------
            container.Page(page =>
            {
                ApplyChrome(page, clinicName, clinicAddress, clinicPhone,
                    isDental, patient, pageNumber: 2, totalPages);

                page.Content().PaddingHorizontal(34).PaddingVertical(10).Column(content =>
                {
                    SectionTitle(content, "Investigations");
                    WritingBox(content, 54);

                    SectionTitle(content, isDental
                        ? "Previous dental treatment (if any, please specify)"
                        : "Previous treatment / hospitalisation (if any, please specify)");
                    WritingBox(content, 44);

                    SectionTitle(content, "Findings & treatment plan");
                    content.Item().Table(table =>
                    {
                        table.ColumnsDefinition(cols =>
                        {
                            cols.ConstantColumn(28);
                            cols.RelativeColumn(3);
                            cols.RelativeColumn(3);
                            cols.ConstantColumn(66);
                        });
                        table.Header(h =>
                        {
                            foreach (var title in new[]
                                { "SL", "CLINICAL / RADIOGRAPHIC FINDINGS", "TREATMENT ADVISED", "SELECTED" })
                                h.Cell().Background(Ink).Padding(5)
                                    .Text(title).FontSize(7).SemiBold().FontColor(Colors.White);
                        });
                        for (var i = 0; i < 6; i++)
                            for (var col = 0; col < 4; col++)
                                table.Cell().Border(0.8f).BorderColor(Border).Height(26);
                    });

                    content.Item().PaddingTop(8).Text(t =>
                    {
                        t.Span("Initial estimated cost of selected treatment:  ").FontSize(9).SemiBold();
                        t.Span("₹ ____________________").FontSize(9.5f);
                    });

                    // Informed consent — Malayalam version arrives with i18n
                    content.Item().PaddingTop(12).AlignCenter().Column(c =>
                    {
                        c.Item().AlignCenter().Text("PATIENT TREATMENT INFORMED CONSENT")
                            .FontSize(10).SemiBold();
                        c.Item().AlignCenter().PaddingTop(3).Width(60)
                            .LineHorizontal(2).LineColor(TealBright);
                    });
                    content.Item().PaddingTop(8).Text(
                        "I have been fully informed of the nature of the procedures involved in the treatment " +
                        $"of my {(isDental ? "dental" : "medical")} conditions, the procedures to be utilized, the risks and benefits of the " +
                        "treatment, the anesthesia selected, and the necessity of follow-up and self-care. The " +
                        "treatments have been decided in consultation with me after analysing the risks, " +
                        "benefits and the costs involved.").FontSize(8.5f).LineHeight(1.5f).FontColor("#2E4A41");
                    content.Item().PaddingTop(5).Text(
                        "I have had the opportunity to ask any questions I may have in connection with the " +
                        "treatment and to discuss my concerns with the Doctor. I hereby consent to the " +
                        "performance of the procedures as presented to me during consultation and the " +
                        "treatment plan described in this document.").FontSize(8.5f).LineHeight(1.5f).FontColor("#2E4A41");
                    content.Item().PaddingTop(6).Text(
                        "I CERTIFY THAT I HAVE READ AND FULLY UNDERSTAND THIS CONSENT DOCUMENT.")
                        .FontSize(8.5f).SemiBold();

                    content.Item().PaddingTop(14).Row(r =>
                    {
                        r.RelativeItem().Column(c =>
                        {
                            c.Item().LineHorizontal(0.9f).LineColor(Ink);
                            c.Item().PaddingTop(3).Text("Name of patient / guardian")
                                .FontSize(7.5f).FontColor(Muted);
                        });
                        r.ConstantItem(30);
                        r.RelativeItem().Column(c =>
                        {
                            c.Item().LineHorizontal(0.9f).LineColor(Ink);
                            c.Item().PaddingTop(3).Text("Signature").FontSize(7.5f).FontColor(Muted);
                        });
                        r.ConstantItem(30);
                        r.ConstantItem(110).Column(c =>
                        {
                            c.Item().Text($"Date: {DateTime.UtcNow:dd/MM/yyyy}").FontSize(9);
                        });
                    });
                });
            });

            // ---------- Extra pages: the clinic's OWN sections (form builder) ----------
            if (sections.Count > 0)
            {
                var chunks = ChunkSections(sections);
                for (var i = 0; i < chunks.Count; i++)
                {
                    var chunk = chunks[i];
                    var pageNumber = 3 + i;
                    container.Page(page =>
                    {
                        ApplyChrome(page, clinicName, clinicAddress, clinicPhone,
                            isDental, patient, pageNumber, totalPages);

                        page.Content().PaddingHorizontal(34).PaddingVertical(10).Column(content =>
                        {
                            content.Item().Text("Additional sections")
                                .FontSize(8).SemiBold().FontColor(Muted);

                            foreach (var section in chunk)
                            {
                                var answer = answers?.Custom
                                    .FirstOrDefault(a => a.SectionId == section.Id);

                                SectionTitle(content, section.Title);
                                switch (section.Kind)
                                {
                                    case "box":
                                        WritingBox(content, 64, answer?.Text);
                                        break;

                                    case "lines":
                                        content.Item().Border(1).BorderColor(Border).Padding(9).Column(c =>
                                        {
                                            foreach (var item in section.Items)
                                            {
                                                var value = answer?.Lines.GetValueOrDefault(item);
                                                c.Item().PaddingTop(7).Row(r =>
                                                {
                                                    r.ConstantItem(120).Text(item).FontSize(8.5f);
                                                    if (string.IsNullOrWhiteSpace(value))
                                                        r.RelativeItem().PaddingTop(9)
                                                            .LineHorizontal(0.8f).LineColor(Border);
                                                    else
                                                        r.RelativeItem().Text(Truncate(value, 90))
                                                            .FontSize(8.5f).SemiBold();
                                                });
                                            }
                                        });
                                        break;

                                    case "checklist":
                                        content.Item().Border(1).BorderColor(Border).Padding(9).Row(r =>
                                        {
                                            var half = (section.Items.Count + 1) / 2;
                                            foreach (var column in new[]
                                                { section.Items.Take(half), section.Items.Skip(half) })
                                            {
                                                r.RelativeItem().Column(c =>
                                                {
                                                    foreach (var item in column)
                                                        CheckItem(c, item,
                                                            answer?.Checked.Contains(item) == true);
                                                });
                                            }
                                        });
                                        break;
                                }
                            }
                        });
                    });
                }
            }
        }).GeneratePdf();
    }

    /// <summary>Shared page chrome: A4, ink letterhead band, footer band.</summary>
    private static void ApplyChrome(
        PageDescriptor page, string clinicName, string? clinicAddress, string? clinicPhone,
        bool isDental, PatientDto patient, int pageNumber, int totalPages)
    {
        page.Size(PageSizes.A4);
        page.Margin(0);
        page.DefaultTextStyle(t => t.FontSize(9.5f).FontColor(Ink));

        page.Header().Background(Ink).PaddingHorizontal(34).PaddingVertical(14).Row(row =>
        {
            row.RelativeItem().Column(col =>
            {
                col.Item().Row(brand =>
                {
                    brand.AutoItem().Width(19).Height(19).Background(TealBright)
                        .AlignCenter().AlignMiddle()
                        .Text("+").FontSize(13).SemiBold().FontColor("#06362B");
                    brand.AutoItem().PaddingLeft(7).AlignMiddle()
                        .Text(clinicName).FontSize(15).SemiBold().FontColor(Colors.White);
                });
                var contactLine = string.Join("   ·   ",
                    new[] { clinicAddress, clinicPhone }.Where(v => !string.IsNullOrWhiteSpace(v))!);
                if (contactLine.Length > 0)
                    col.Item().PaddingTop(2).Text(contactLine).FontSize(7.5f).FontColor(HeaderSub);
                col.Item().PaddingTop(2)
                    .Text((isDental ? "DENTAL" : "GENERAL") + " PATIENT INTAKE FORM")
                    .FontSize(7).SemiBold().FontColor("#7FC8B4").LetterSpacing(0.15f);
            });
            row.ConstantItem(150).AlignRight().AlignMiddle().Column(col =>
            {
                col.Item().AlignRight().Text($"ID No: P-{patient.PatientNumber:D6}")
                    .FontSize(10.5f).SemiBold().FontColor(Colors.White);
                col.Item().AlignRight().Text($"{DateTime.UtcNow:dd MMM yyyy}")
                    .FontSize(8).FontColor(HeaderSub);
                col.Item().AlignRight()
                    .Text($"{patient.Gender}  ·  {(patient.Age.HasValue ? patient.Age + " yrs" : "Age —")}")
                    .FontSize(8).FontColor(HeaderSub);
            });
        });

        page.Footer().Background(Ink).PaddingHorizontal(34).PaddingVertical(6).Row(row =>
        {
            row.RelativeItem().Text(clinicName).FontSize(7).SemiBold().FontColor(Colors.White);
            row.RelativeItem().AlignRight()
                .Text($"Page {pageNumber} of {totalPages}  ·  powered by Klivia")
                .FontSize(7).FontColor(HeaderSub);
        });
    }

    /// <summary>Uppercase micro-title with a teal accent bar — the section voice.</summary>
    private static void SectionTitle(ColumnDescriptor content, string title)
    {
        content.Item().PaddingTop(6).PaddingBottom(3).Row(r =>
        {
            r.AutoItem().PaddingTop(1).Width(3).Height(10).Background(TealBright);
            r.AutoItem().PaddingLeft(6).Text(title.ToUpperInvariant())
                .FontSize(7.5f).SemiBold().FontColor(Teal).LetterSpacing(0.08f);
        });
    }

    private static void WritingBox(ColumnDescriptor content, float height, string? text = null)
    {
        content.Item().Border(1).BorderColor(Border).Padding(8)
            .Column(c => FilledOrBlank(c, height, text));
    }

    /// <summary>Blank writing space on paper forms; the captured answer when
    /// staff filled it digitally. Text is clamped so pages can't overflow.</summary>
    private static void FilledOrBlank(ColumnDescriptor column, float height, string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            column.Item().Height(height);
        else
            column.Item().MinHeight(height).PaddingTop(2)
                .Text(Truncate(text, (int)(height * 6.5f)))
                .FontSize(9).LineHeight(1.4f).FontColor("#1A362D");
    }

    private static string Truncate(string value, int max)
        => value.Length <= max ? value : value[..max] + "…";

    private static void CheckItem(ColumnDescriptor column, string label, bool ticked = false)
    {
        column.Item().PaddingVertical(2).Row(line =>
        {
            if (ticked)
                line.ConstantItem(10).Height(10).Background(TealBright)
                    .AlignCenter().AlignMiddle()
                    .Text("✓").FontSize(7).SemiBold().FontColor(Colors.White);
            else
                line.ConstantItem(10).Height(10).Border(1.1f).BorderColor(Teal);
            line.ConstantItem(5);
            var text = line.RelativeItem().Text(label).FontSize(8.5f);
            if (ticked) text.SemiBold();
        });
    }

    private static void DottedSection(IContainer container, string title, string[] lines)
    {
        container.Border(1).BorderColor(Border).Padding(8).Column(c =>
        {
            c.Item().Text(title).FontSize(8.5f).SemiBold().FontColor(Teal);
            foreach (var line in lines)
                c.Item().PaddingTop(6).Row(r =>
                {
                    r.ConstantItem(74).Text(line).FontSize(8.5f);
                    r.RelativeItem().PaddingTop(9).LineHorizontal(0.8f).LineColor(Border);
                });
        });
    }

    /// <summary>Rough per-section height estimate → chunk to pages so a
    /// clinic with many custom sections never overflows a fixed page.</summary>
    private static List<List<IntakeFormSectionDto>> ChunkSections(List<IntakeFormSectionDto> sections)
    {
        const float pageBudget = 640f;
        var chunks = new List<List<IntakeFormSectionDto>>();
        var current = new List<IntakeFormSectionDto>();
        var used = 0f;

        foreach (var section in sections)
        {
            var height = section.Kind switch
            {
                "box" => 100f,
                "lines" => 46f + section.Items.Count * 20f,
                "checklist" => 46f + ((section.Items.Count + 1) / 2) * 15f,
                _ => 100f,
            };

            if (used + height > pageBudget && current.Count > 0)
            {
                chunks.Add(current);
                current = [];
                used = 0f;
            }
            current.Add(section);
            used += height;
        }
        if (current.Count > 0) chunks.Add(current);
        return chunks;
    }
}
