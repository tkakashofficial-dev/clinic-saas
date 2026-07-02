using Clinic.Application.Common.Exceptions;
using Clinic.Application.Features.Staff.DTOs;
using Clinic.Domain.Constants;
using Clinic.Infrastructure.Services;
using Clinic.Tests.TestInfrastructure;

namespace Clinic.Tests;

public class StaffServiceTests : IDisposable
{
    private readonly TestDb _db = new();

    private StaffService CreateService() =>
        new(_db.CreateContext(), _db.CurrentUser);

    private static AddStaffRequest ValidStaff(string role, string email) => new()
    {
        FirstName = "Staff",
        LastName = "Member",
        Email = email,
        Password = "Str0ng-Pass-123",
        Role = role
    };

    [Fact]
    public async Task AddStaff_UnknownRole_ThrowsBadRequest()
    {
        var clinic = await _db.SeedTenantAsync("Clinic", "a@a.com");
        _db.CurrentUser.ActAs(clinic.TenantId, clinic.TenantUserId);

        await Assert.ThrowsAsync<BadRequestException>(
            () => CreateService().AddStaffAsync(ValidStaff("Docter", "typo@clinic.com")));
    }

    [Fact]
    public async Task AddStaff_LowercaseRole_NormalizesToCanonical()
    {
        var clinic = await _db.SeedTenantAsync("Clinic", "a@a.com");
        _db.CurrentUser.ActAs(clinic.TenantId, clinic.TenantUserId);

        var staff = await CreateService().AddStaffAsync(ValidStaff("doctor", "doc@clinic.com"));

        Assert.Equal(RoleNames.Doctor, staff.Role);
    }

    [Fact]
    public async Task AddStaff_DuplicateEmail_ThrowsConflict()
    {
        var clinic = await _db.SeedTenantAsync("Clinic", "a@a.com");
        _db.CurrentUser.ActAs(clinic.TenantId, clinic.TenantUserId);

        await CreateService().AddStaffAsync(ValidStaff("Receptionist", "same@clinic.com"));

        await Assert.ThrowsAsync<ConflictException>(
            () => CreateService().AddStaffAsync(ValidStaff("Doctor", "same@clinic.com")));
    }

    public void Dispose() => _db.Dispose();
}
