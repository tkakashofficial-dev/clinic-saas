using Clinic.Application.Common.Exceptions;
using Clinic.Application.Common.Models;
using Clinic.Application.Features.Patients.DTOs;
using Clinic.Infrastructure.Services;
using Clinic.Tests.TestInfrastructure;

namespace Clinic.Tests;

public class PatientServiceTests : IDisposable
{
    private readonly TestDb _db = new();

    private PatientService CreateService() =>
        new(_db.CreateContext(), _db.CurrentUser);

    private static RegisterPatientRequest ValidPatient(
        string firstName = "John", string phone = "+31612345678") => new()
    {
        FirstName = firstName,
        LastName = "Doe",
        Phone = phone,
        Gender = "Male",
        DateOfBirth = new DateOnly(1985, 3, 15)
    };

    [Fact]
    public async Task RegisterPatient_DuplicatePhone_SameTenant_ThrowsConflict()
    {
        var clinic = await _db.SeedTenantAsync("Clinic", "a@a.com");
        _db.CurrentUser.ActAs(clinic.TenantId, clinic.TenantUserId);

        await CreateService().RegisterPatientAsync(ValidPatient(phone: "+31600000001"));

        await Assert.ThrowsAsync<ConflictException>(
            () => CreateService().RegisterPatientAsync(ValidPatient(phone: "+31600000001")));
    }

    [Fact]
    public async Task RegisterPatient_SamePhone_DifferentTenant_Succeeds()
    {
        // The unique index is (TenantId, Phone) — two clinics can both have
        // a patient with the same phone number
        var clinicA = await _db.SeedTenantAsync("Clinic A", "a@a.com");
        var clinicB = await _db.SeedTenantAsync("Clinic B", "b@b.com");

        _db.CurrentUser.ActAs(clinicA.TenantId, clinicA.TenantUserId);
        await CreateService().RegisterPatientAsync(ValidPatient(phone: "+31600000002"));

        _db.CurrentUser.ActAs(clinicB.TenantId, clinicB.TenantUserId);
        var patient = await CreateService().RegisterPatientAsync(ValidPatient(phone: "+31600000002"));

        Assert.NotEqual(Guid.Empty, patient.Id);
    }

    [Fact]
    public async Task RegisterPatient_InvalidGender_ThrowsBadRequest()
    {
        var clinic = await _db.SeedTenantAsync("Clinic", "a@a.com");
        _db.CurrentUser.ActAs(clinic.TenantId, clinic.TenantUserId);

        var request = ValidPatient();
        request.Gender = "NotAGender";

        await Assert.ThrowsAsync<BadRequestException>(
            () => CreateService().RegisterPatientAsync(request));
    }

    [Fact]
    public async Task GetAllPatients_ReturnsCorrectPage_AndTotals()
    {
        var clinic = await _db.SeedTenantAsync("Clinic", "a@a.com");
        _db.CurrentUser.ActAs(clinic.TenantId, clinic.TenantUserId);

        for (var i = 0; i < 25; i++)
            await CreateService().RegisterPatientAsync(
                ValidPatient(firstName: $"Patient{i:D2}", phone: $"+3161000{i:D4}"));

        var page2 = await CreateService().GetAllPatientsAsync(
            search: null, new PageRequest(Page: 2, PageSize: 10));

        Assert.Equal(10, page2.Items.Count);
        Assert.Equal(25, page2.TotalCount);
        Assert.Equal(3, page2.TotalPages);
        Assert.True(page2.HasNextPage);
        Assert.True(page2.HasPreviousPage);
    }

    [Fact]
    public async Task GetAllPatients_Search_FiltersByName()
    {
        var clinic = await _db.SeedTenantAsync("Clinic", "a@a.com");
        _db.CurrentUser.ActAs(clinic.TenantId, clinic.TenantUserId);

        await CreateService().RegisterPatientAsync(ValidPatient("Alice", "+31600000010"));
        await CreateService().RegisterPatientAsync(ValidPatient("Bob", "+31600000011"));

        var result = await CreateService().GetAllPatientsAsync("alice", new PageRequest());

        Assert.Single(result.Items);
        Assert.StartsWith("Alice", result.Items[0].FullName);
    }

    [Fact]
    public async Task RegisterPatient_AssignsSequentialNumbersPerClinic()
    {
        var clinicA = await _db.SeedTenantAsync("Clinic A", "a@a.com");
        var clinicB = await _db.SeedTenantAsync("Clinic B", "b@b.com");

        _db.CurrentUser.ActAs(clinicA.TenantId, clinicA.TenantUserId);
        var first = await CreateService().RegisterPatientAsync(ValidPatient("One", "+31600000060"));
        var second = await CreateService().RegisterPatientAsync(ValidPatient("Two", "+31600000061"));

        // Each clinic gets its OWN sequence starting at 1
        _db.CurrentUser.ActAs(clinicB.TenantId, clinicB.TenantUserId);
        var otherClinic = await CreateService().RegisterPatientAsync(ValidPatient("Uno", "+31600000062"));

        Assert.Equal(1, first.PatientNumber);
        Assert.Equal(2, second.PatientNumber);
        Assert.Equal(1, otherClinic.PatientNumber);
    }

    [Fact]
    public async Task IntakeForm_GeneratesBrandedPdf()
    {
        var clinic = await _db.SeedTenantAsync("Clinic", "a@a.com");
        _db.CurrentUser.ActAs(clinic.TenantId, clinic.TenantUserId);
        var patient = await CreateService().RegisterPatientAsync(ValidPatient("Form", "+31600000070"));

        var (content, fileName) = await CreateService().GetIntakeFormPdfAsync(patient.Id);

        Assert.True(content.Length > 1000);
        Assert.StartsWith("%PDF", System.Text.Encoding.ASCII.GetString(content[..4]));
        Assert.Contains("P000001", fileName);
    }

    [Fact]
    public async Task UpdatePatient_CorrectsDetails()
    {
        var clinic = await _db.SeedTenantAsync("Clinic", "a@a.com");
        _db.CurrentUser.ActAs(clinic.TenantId, clinic.TenantUserId);

        var created = await CreateService().RegisterPatientAsync(
            ValidPatient("Jhon", "+31600000030")); // reception typo!

        var updated = await CreateService().UpdatePatientAsync(created.Id,
            new UpdatePatientRequest
            {
                FirstName = "John",
                LastName = "Doe",
                Phone = "+31600000031",
                Gender = "Male",
                DateOfBirth = new DateOnly(1985, 3, 15),
            });

        Assert.Equal("John", updated.FirstName);
        Assert.Equal("+31600000031", updated.Phone);
    }

    [Fact]
    public async Task UpdatePatient_PhoneOfAnotherPatient_ThrowsConflict()
    {
        var clinic = await _db.SeedTenantAsync("Clinic", "a@a.com");
        _db.CurrentUser.ActAs(clinic.TenantId, clinic.TenantUserId);

        await CreateService().RegisterPatientAsync(ValidPatient("Alice", "+31600000040"));
        var bob = await CreateService().RegisterPatientAsync(ValidPatient("Bob", "+31600000041"));

        // Bob cannot take Alice's number — but keeping his own must be fine
        await Assert.ThrowsAsync<ConflictException>(
            () => CreateService().UpdatePatientAsync(bob.Id, new UpdatePatientRequest
            {
                FirstName = "Bob", LastName = "Doe", Phone = "+31600000040", Gender = "Male",
            }));

        var keepOwn = await CreateService().UpdatePatientAsync(bob.Id, new UpdatePatientRequest
        {
            FirstName = "Robert", LastName = "Doe", Phone = "+31600000041", Gender = "Male",
        });
        Assert.Equal("Robert", keepOwn.FirstName);
    }

    [Fact]
    public async Task UpdatePatient_FromAnotherTenant_ThrowsNotFound()
    {
        var clinicA = await _db.SeedTenantAsync("Clinic A", "a@a.com");
        var clinicB = await _db.SeedTenantAsync("Clinic B", "b@b.com");

        _db.CurrentUser.ActAs(clinicA.TenantId, clinicA.TenantUserId);
        var patient = await CreateService().RegisterPatientAsync(ValidPatient(phone: "+31600000050"));

        _db.CurrentUser.ActAs(clinicB.TenantId, clinicB.TenantUserId);
        await Assert.ThrowsAsync<NotFoundException>(
            () => CreateService().UpdatePatientAsync(patient.Id, new UpdatePatientRequest
            {
                FirstName = "Hacked", LastName = "Name", Phone = "+31600000050", Gender = "Male",
            }));
    }

    [Fact]
    public async Task GetPatientById_FromAnotherTenant_ThrowsNotFound()
    {
        var clinicA = await _db.SeedTenantAsync("Clinic A", "a@a.com");
        var clinicB = await _db.SeedTenantAsync("Clinic B", "b@b.com");

        _db.CurrentUser.ActAs(clinicA.TenantId, clinicA.TenantUserId);
        var patient = await CreateService().RegisterPatientAsync(ValidPatient(phone: "+31600000020"));

        // Clinic B asks for clinic A's patient by its real id
        _db.CurrentUser.ActAs(clinicB.TenantId, clinicB.TenantUserId);
        await Assert.ThrowsAsync<NotFoundException>(
            () => CreateService().GetPatientByIdAsync(patient.Id));
    }

    public void Dispose() => _db.Dispose();
}
