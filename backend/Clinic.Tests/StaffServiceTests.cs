using Clinic.Application.Common.Exceptions;
using Clinic.Application.Features.Staff.DTOs;
using Clinic.Domain.Constants;
using Clinic.Infrastructure.Services;
using Clinic.Tests.TestInfrastructure;
using Microsoft.EntityFrameworkCore;
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
    public async Task AddStaff_WithoutPassword_CreatesInviteOnlyAccount()
    {
        // Invite-only: no temp password; the account works only via the
        // emailed set-your-password link
        var clinic = await _db.SeedTenantAsync("Clinic", "a@a.com");
        _db.CurrentUser.ActAs(clinic.TenantId, clinic.TenantUserId);

        var request = ValidStaff("invited@clinic.com", "Doctor");
        request.Password = null;

        var staff = await CreateService().AddStaffAsync(request);
        Assert.NotEqual(Guid.Empty, staff.Id);

        await using var context = _db.CreateContext();
        var inviteTokens = await context.PasswordResetTokens
            .CountAsync(t => t.SystemUserId == staff.SystemUserId && t.UsedAt == null);
        Assert.Equal(1, inviteTokens); // the set-your-password invite exists
    }

    [Fact]
    public async Task AddStaff_SameEmailSameClinic_ThrowsConflict()
    {
        var clinic = await _db.SeedTenantAsync("Clinic", "a@a.com");
        _db.CurrentUser.ActAs(clinic.TenantId, clinic.TenantUserId);

        await CreateService().AddStaffAsync(ValidStaff("same@clinic.com", "Receptionist"));

        await Assert.ThrowsAsync<ConflictException>(
            () => CreateService().AddStaffAsync(ValidStaff("same@clinic.com", "Doctor")));
    }

    [Fact]
    public async Task AddStaff_DoctorFromAnotherClinic_AttachesMembership_PasswordUntouched()
    {
        // The visiting-doctor case: one account, two clinics
        var clinicA = await _db.SeedTenantAsync("Clinic A", "visiting@doctor.com");
        var clinicB = await _db.SeedTenantAsync("Clinic B", "ownerb@clinic.com");

        string hashBefore;
        await using (var context = _db.CreateContext())
            hashBefore = (await context.SystemUsers.FirstAsync(
                u => u.Email == "visiting@doctor.com")).PasswordHash;

        // Clinic B's admin adds the doctor who already works at clinic A —
        // the temp password typed by B's admin MUST be ignored
        _db.CurrentUser.ActAs(clinicB.TenantId, clinicB.TenantUserId);
        var staff = await CreateService().AddStaffAsync(
            ValidStaff("visiting@doctor.com", "Doctor"));

        Assert.True(staff.ExistingAccount);

        await using var verify = _db.CreateContext();
        var user = await verify.SystemUsers.FirstAsync(u => u.Email == "visiting@doctor.com");
        Assert.Equal(hashBefore, user.PasswordHash); // B's admin has no power over it

        var memberships = await verify.TenantUsers
            .IgnoreQueryFilters()
            .CountAsync(tu => tu.SystemUserId == user.Id);
        Assert.Equal(2, memberships); // clinic A + clinic B

        var inviteTokens = await verify.PasswordResetTokens
            .CountAsync(t => t.SystemUserId == user.Id);
        Assert.Equal(0, inviteTokens); // no set-password link for existing accounts
    }

    public void Dispose() => _db.Dispose();
}
