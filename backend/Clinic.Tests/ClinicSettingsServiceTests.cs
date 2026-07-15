using Clinic.Application.Common.Exceptions;
using Clinic.Application.Features.Settings.DTOs;
using Clinic.Infrastructure.Services;
using Clinic.Tests.TestInfrastructure;

namespace Clinic.Tests;

/// <summary>
/// Clinic settings: the letterhead (name/phone/address on every PDF) and the
/// default intake template. Admin edits; template values are a closed set.
/// </summary>
public class ClinicSettingsServiceTests : IDisposable
{
    private readonly TestDb _db = new();

    private ClinicSettingsService CreateService() =>
        new(_db.CreateContext(), _db.CurrentUser);

    private async Task ActAsClinicAdminAsync(string name = "Smile Dental")
    {
        var clinic = await _db.SeedTenantAsync(name, "admin@clinic.com");
        _db.CurrentUser.ActAs(clinic.TenantId, clinic.TenantUserId);
    }

    [Fact]
    public async Task Get_NewClinic_DefaultsToDentalTemplate()
    {
        await ActAsClinicAdminAsync();

        var settings = await CreateService().GetAsync();

        Assert.Equal("Smile Dental", settings.Name);
        Assert.Equal("dental", settings.DefaultIntakeTemplate);
    }

    [Fact]
    public async Task Update_PersistsLetterheadAndTemplate()
    {
        await ActAsClinicAdminAsync();

        await CreateService().UpdateAsync(new UpdateClinicSettingsRequest
        {
            Name = "Smile Dental — Nadapuram",
            Phone = "+91 98765 43210",
            Address = "Main Road, Nadapuram, Kozhikode",
            DefaultIntakeTemplate = "General",   // case-insensitive on purpose
        });

        var settings = await CreateService().GetAsync();
        Assert.Equal("Smile Dental — Nadapuram", settings.Name);
        Assert.Equal("+91 98765 43210", settings.Phone);
        Assert.Equal("general", settings.DefaultIntakeTemplate);
    }

    [Fact]
    public async Task Update_UnknownTemplate_IsRejected()
    {
        await ActAsClinicAdminAsync();

        await Assert.ThrowsAsync<BadRequestException>(
            () => CreateService().UpdateAsync(new UpdateClinicSettingsRequest
            {
                Name = "Smile Dental",
                DefaultIntakeTemplate = "cardio",
            }));
    }

    [Fact]
    public async Task Update_BlankName_IsRejected()
    {
        // The name prints on every prescription — it can never be empty
        await ActAsClinicAdminAsync();

        await Assert.ThrowsAsync<BadRequestException>(
            () => CreateService().UpdateAsync(new UpdateClinicSettingsRequest
            {
                Name = "   ",
                DefaultIntakeTemplate = "dental",
            }));
    }

    public void Dispose() => _db.Dispose();
}
