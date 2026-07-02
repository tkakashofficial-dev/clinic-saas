using Clinic.Application.Common.Exceptions;
using Clinic.Application.Features.Staff.DTOs;
using Clinic.Domain.Constants;
using Clinic.Infrastructure.Services;
using Clinic.Tests.TestInfrastructure;
using Microsoft.Extensions.Options;

namespace Clinic.Tests;

public class StaffServiceTests : IDisposable
{
    private readonly TestDb _db = new();

    private StaffService CreateService() =>
        new(_db.CreateContext(), _db.CurrentUser, new NoOpEmailSender(), Options.Create(new FrontendSettings()));

    private static AddStaffRequest ValidStaff(string email, params string[] roles) => new()
    {
        FirstName = "Staff",
        LastName = "Member",
        Email = email,
        Password = "Str0ng-Pass-123",
        Roles = roles.ToList()
    };

    [Fact]
    public async Task AddStaff_UnknownRole_ThrowsBadRequest()
    {
        var clinic = await _db.SeedTenantAsync("Clinic", "a@a.com");
        _db.CurrentUser.ActAs(clinic.TenantId, clinic.TenantUserId);

        await Assert.ThrowsAsync<BadRequestException>(
            () => CreateService().AddStaffAsync(ValidStaff("typo@clinic.com", "Docter")));
    }

    [Fact]
    public async Task AddStaff_LowercaseRole_NormalizesToCanonical()
    {
        var clinic = await _db.SeedTenantAsync("Clinic", "a@a.com");
        _db.CurrentUser.ActAs(clinic.TenantId, clinic.TenantUserId);

        var staff = await CreateService().AddStaffAsync(ValidStaff("doc@clinic.com", "doctor"));

        Assert.Equal([RoleNames.Doctor], staff.Roles);
    }

    [Fact]
    public async Task AddStaff_MultipleRoles_PartnerWhoPractices()
    {
        // A partner who also treats patients: Admin + Doctor on one account
        var clinic = await _db.SeedTenantAsync("Clinic", "a@a.com");
        _db.CurrentUser.ActAs(clinic.TenantId, clinic.TenantUserId);

        var staff = await CreateService().AddStaffAsync(
            ValidStaff("partner@clinic.com", "Admin", "Doctor"));

        Assert.Equal(2, staff.Roles.Count);
        Assert.Contains(RoleNames.Admin, staff.Roles);
        Assert.Contains(RoleNames.Doctor, staff.Roles);
    }

    [Fact]
    public async Task AddStaff_NoRoles_ThrowsBadRequest()
    {
        var clinic = await _db.SeedTenantAsync("Clinic", "a@a.com");
        _db.CurrentUser.ActAs(clinic.TenantId, clinic.TenantUserId);

        await Assert.ThrowsAsync<BadRequestException>(
            () => CreateService().AddStaffAsync(ValidStaff("norole@clinic.com")));
    }

    [Fact]
    public async Task AddStaff_DuplicateEmail_ThrowsConflict()
    {
        var clinic = await _db.SeedTenantAsync("Clinic", "a@a.com");
        _db.CurrentUser.ActAs(clinic.TenantId, clinic.TenantUserId);

        await CreateService().AddStaffAsync(ValidStaff("same@clinic.com", "Receptionist"));

        await Assert.ThrowsAsync<ConflictException>(
            () => CreateService().AddStaffAsync(ValidStaff("same@clinic.com", "Doctor")));
    }

    public void Dispose() => _db.Dispose();
}
