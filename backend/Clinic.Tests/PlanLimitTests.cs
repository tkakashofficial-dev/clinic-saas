using Clinic.Application.Common.Exceptions;
using Clinic.Application.Features.Billing.DTOs;
using Clinic.Application.Features.Staff.DTOs;
using Clinic.Domain.Enums;
using Clinic.Infrastructure.Services;
using Clinic.Tests.TestInfrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Clinic.Tests;

public class PlanLimitTests : IDisposable
{
    private readonly TestDb _db = new();

    private StaffService CreateStaffService() =>
        new(_db.CreateContext(), _db.CurrentUser, new NoOpEmailSender(),
            Options.Create(new FrontendSettings()));

    private BillingService CreateBillingService() =>
        new(_db.CreateContext(), _db.CurrentUser);

    private static AddStaffRequest Staff(string email, params string[] roles) => new()
    {
        FirstName = "Staff",
        LastName = "Member",
        Email = email,
        Password = "Str0ng-Pass-123",
        Roles = roles.ToList()
    };

    private async Task SetPlanAsync(Guid tenantId, PlanType plan)
    {
        await using var context = _db.CreateContext();
        var tenant = await context.Tenants.IgnoreQueryFilters().FirstAsync(t => t.Id == tenantId);
        tenant.ChangePlan(plan);
        await context.SaveChangesAsync();
    }

    [Fact]
    public async Task SoloPlan_OneDoctorAllowed_SecondBlocked()
    {
        var clinic = await _db.SeedTenantAsync("Solo Clinic", "solo@clinic.com");
        await SetPlanAsync(clinic.TenantId, PlanType.Solo);
        _db.CurrentUser.ActAs(clinic.TenantId, clinic.TenantUserId);

        // Admin (non-doctor) + 1 doctor = exactly the Solo allowance
        var first = await CreateStaffService().AddStaffAsync(Staff("doc1@clinic.com", "Doctor"));
        Assert.NotEqual(Guid.Empty, first.Id);

        // A second doctor exceeds the plan (staff AND doctor limits)
        await Assert.ThrowsAsync<PlanLimitException>(
            () => CreateStaffService().AddStaffAsync(Staff("doc2@clinic.com", "Doctor")));
    }

    [Fact]
    public async Task SoloPlan_ThirdStaffMember_IsBlocked()
    {
        var clinic = await _db.SeedTenantAsync("Solo Clinic", "solo2@clinic.com");
        await SetPlanAsync(clinic.TenantId, PlanType.Solo);
        _db.CurrentUser.ActAs(clinic.TenantId, clinic.TenantUserId);

        await CreateStaffService().AddStaffAsync(Staff("rec@clinic.com", "Receptionist")); // 2/2

        await Assert.ThrowsAsync<PlanLimitException>(
            () => CreateStaffService().AddStaffAsync(Staff("rec2@clinic.com", "Receptionist")));
    }

    [Fact]
    public async Task TrialClinic_GetsClinicTierLimits()
    {
        // New tenants are in trial: Clinic-tier limits apply even before paying
        var clinic = await _db.SeedTenantAsync("Trial Clinic", "trial@clinic.com");
        _db.CurrentUser.ActAs(clinic.TenantId, clinic.TenantUserId);

        // Adding a 2nd and 3rd staff member must work (Solo would block at 2)
        await CreateStaffService().AddStaffAsync(Staff("d1@clinic.com", "Doctor"));
        var third = await CreateStaffService().AddStaffAsync(Staff("r1@clinic.com", "Receptionist"));

        Assert.NotEqual(Guid.Empty, third.Id);
    }

    [Fact]
    public async Task Downgrade_BelowCurrentUsage_IsBlocked()
    {
        var clinic = await _db.SeedTenantAsync("Busy Clinic", "busy@clinic.com");
        _db.CurrentUser.ActAs(clinic.TenantId, clinic.TenantUserId);

        await CreateStaffService().AddStaffAsync(Staff("a@clinic.com", "Doctor"));
        await CreateStaffService().AddStaffAsync(Staff("b@clinic.com", "Receptionist")); // 3 staff now

        await Assert.ThrowsAsync<PlanLimitException>(
            () => CreateBillingService().ChangePlanAsync(new ChangePlanRequest { Plan = "Solo" }));
    }

    [Fact]
    public async Task ChangePlan_EndsTrial_AndSummaryReflectsIt()
    {
        var clinic = await _db.SeedTenantAsync("Upgrader", "up@clinic.com");
        _db.CurrentUser.ActAs(clinic.TenantId, clinic.TenantUserId);

        var summary = await CreateBillingService().ChangePlanAsync(
            new ChangePlanRequest { Plan = "Growth" });

        Assert.Equal("Growth", summary.Plan);
        Assert.False(summary.IsInTrial);
        Assert.Equal(int.MaxValue, summary.MaxStaff);
    }

    public void Dispose() => _db.Dispose();
}
