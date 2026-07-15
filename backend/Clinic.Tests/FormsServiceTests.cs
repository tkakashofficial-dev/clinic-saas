using Clinic.Application.Common.Exceptions;
using Clinic.Application.Features.Forms.DTOs;
using Clinic.Infrastructure.Services;
using Clinic.Tests.TestInfrastructure;

namespace Clinic.Tests;

/// <summary>
/// The form builder (v1): custom sections are validated against a closed set
/// of shapes, stay tenant-isolated, reorder correctly, and render into the
/// intake PDF's extra pages.
/// </summary>
public class FormsServiceTests : IDisposable
{
    private readonly TestDb _db = new();

    private FormsService CreateService() => new(_db.CreateContext(), _db.CurrentUser);

    private async Task ActAsClinicAdminAsync(string name = "Smile Dental", string email = "admin@clinic.com")
    {
        var clinic = await _db.SeedTenantAsync(name, email);
        _db.CurrentUser.ActAs(clinic.TenantId, clinic.TenantUserId);
    }

    [Fact]
    public async Task Create_List_Move_Delete_Roundtrip()
    {
        await ActAsClinicAdminAsync();

        var first = await CreateService().CreateSectionAsync(new SaveIntakeFormSectionRequest
        {
            Kind = "checklist", Title = "Habits", Template = "both",
            Items = ["Smoking", "Pan chewing", "Alcohol"],
        });
        await CreateService().CreateSectionAsync(new SaveIntakeFormSectionRequest
        {
            Kind = "box", Title = "Insurance details", Template = "dental",
        });

        var sections = await CreateService().GetSectionsAsync();
        Assert.Equal(2, sections.Count);
        Assert.Equal("Habits", sections[0].Title);
        Assert.Equal(3, sections[0].Items.Count);

        // Move "Habits" down — order flips
        var reordered = await CreateService().MoveSectionAsync(first.Id, 1);
        Assert.Equal("Insurance details", reordered[0].Title);

        await CreateService().DeleteSectionAsync(first.Id);
        Assert.Single(await CreateService().GetSectionsAsync());
    }

    [Fact]
    public async Task Create_GarbageShapes_AreRejected()
    {
        await ActAsClinicAdminAsync();
        var service = CreateService();

        await Assert.ThrowsAsync<BadRequestException>(() => service.CreateSectionAsync(
            new SaveIntakeFormSectionRequest { Kind = "table", Title = "X" }));
        await Assert.ThrowsAsync<BadRequestException>(() => service.CreateSectionAsync(
            new SaveIntakeFormSectionRequest { Kind = "box", Title = "  " }));
        await Assert.ThrowsAsync<BadRequestException>(() => service.CreateSectionAsync(
            new SaveIntakeFormSectionRequest { Kind = "checklist", Title = "Empty", Items = [] }));
        await Assert.ThrowsAsync<BadRequestException>(() => service.CreateSectionAsync(
            new SaveIntakeFormSectionRequest { Kind = "box", Title = "X", Template = "cardio" }));
    }

    [Fact]
    public async Task Sections_AreInvisibleToOtherClinics()
    {
        await ActAsClinicAdminAsync("Clinic A", "a@clinic.com");
        await CreateService().CreateSectionAsync(new SaveIntakeFormSectionRequest
        {
            Kind = "box", Title = "Clinic A private section",
        });

        // Drop back to anonymous so seeding clinic B doesn't trip the
        // cross-tenant write guard (we're still "signed into" clinic A)
        _db.CurrentUser.ActAsAnonymous();
        var clinicB = await _db.SeedTenantAsync("Clinic B", "b@clinic.com");
        _db.CurrentUser.ActAs(clinicB.TenantId, clinicB.TenantUserId);

        Assert.Empty(await CreateService().GetSectionsAsync());
    }

    [Fact]
    public async Task Preview_RendersPdf_WithCustomSections()
    {
        await ActAsClinicAdminAsync();
        await CreateService().CreateSectionAsync(new SaveIntakeFormSectionRequest
        {
            Kind = "lines", Title = "Allergy details", Template = "both",
            Items = ["Food allergies", "Drug allergies"],
        });

        var (content, fileName) = await CreateService().PreviewPdfAsync("dental");

        Assert.True(content.Length > 2000);
        Assert.StartsWith("%PDF", System.Text.Encoding.ASCII.GetString(content, 0, 4));
        Assert.Equal("intake-dental-preview.pdf", fileName);

        // "both" sections apply to the general template too
        var general = await CreateService().PreviewPdfAsync("general");
        Assert.True(general.Content.Length > 2000);
    }

    [Fact]
    public async Task DigitalFill_SavesAnswers_AndPrintsPreFilled()
    {
        // Staff asked the patient and ticked/typed the answers — they must
        // land on the record and render into the PDF without blowing up
        var clinic = await _db.SeedTenantAsync("Smile Dental", "admin@clinic.com");
        _db.CurrentUser.ActAs(clinic.TenantId, clinic.TenantUserId);

        var patients = new PatientService(_db.CreateContext(), _db.CurrentUser);
        var patient = await patients.RegisterPatientAsync(
            new Clinic.Application.Features.Patients.DTOs.RegisterPatientRequest
            {
                FirstName = "Ayisha", LastName = "Basheer",
                Phone = "+91 90000 11111", Gender = "Female",
            });

        var saved = await CreateService().SaveResponseAsync(patient.Id,
            new SaveIntakeFormResponseRequest
            {
                Template = "dental",
                Answers = new IntakeAnswersDto
                {
                    DiseaseChecklist = ["Diabetic", "Drug Allergies"],
                    ChiefComplaint = "Tooth pain, lower left, 3 days",
                    Medications = "Metformin 500mg",
                },
            });

        Assert.Equal("dental", saved.Template);
        Assert.Contains("Diabetic", saved.Answers.DiseaseChecklist);
        Assert.Equal("Test Admin", saved.FilledByName);

        var latest = await CreateService().GetLatestResponseAsync(patient.Id);
        Assert.NotNull(latest);
        Assert.Equal("Tooth pain, lower left, 3 days", latest!.Answers.ChiefComplaint);

        // The intake PDF renders the answers (matching template) without error
        var (pdf, _) = await patients.GetIntakeFormPdfAsync(patient.Id, "dental");
        Assert.StartsWith("%PDF", System.Text.Encoding.ASCII.GetString(pdf, 0, 4));

        // A patient who never filled digitally → 404-safe null
        Assert.Null(await CreateService().GetLatestResponseAsync(Guid.NewGuid()));
    }

    public void Dispose() => _db.Dispose();
}
