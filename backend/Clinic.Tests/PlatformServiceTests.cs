using Clinic.Application.Common.Exceptions;
using Clinic.Application.Features.Auth.DTOs;
using Clinic.Application.Features.Platform.DTOs;
using Clinic.Infrastructure.Services;
using Clinic.Tests.TestInfrastructure;
using Microsoft.Extensions.Options;

namespace Clinic.Tests;

/// <summary>
/// The SaaS owner's console: access is an email allowlist (NOT clinic roles),
/// plan changes are applied after manual payment, and suspension must lock
/// every user of that clinic out at the next sign-in.
/// </summary>
public class PlatformServiceTests : IDisposable
{
    private const string OwnerEmail = "owner@klivia.com";

    private readonly TestDb _db = new();

    private readonly NoOpEmailSender _emails = new();

    private PlatformService CreatePlatformService() =>
        new(_db.CreateContext(), _db.CurrentUser, _emails,
            Options.Create(new PlatformSettings { AdminEmails = [OwnerEmail] }),
            Options.Create(new EmailSettings { User = "test@clinic.com", Password = "x" }),
            Options.Create(new BrevoSettings()));

    private AuthService CreateAuthService(string? platformAdmin = OwnerEmail) =>
        new(_db.CreateContext(), new JwtTokenGenerator(Options.Create(new JwtSettings
        {
            Secret = new string('k', 64),
            Issuer = "TestIssuer",
            Audience = "TestAudience",
            ExpiresInMinutes = 60
        })), new NoOpEmailSender(), Options.Create(new FrontendSettings()),
        Options.Create(new PlatformSettings
        {
            AdminEmails = platformAdmin is null ? [] : [platformAdmin]
        }));

    private void ActAsPlatformOwner()
    {
        _db.CurrentUser.ActAs(Guid.Empty, Guid.Empty);
        _db.CurrentUser.Email = OwnerEmail;
    }

    [Fact]
    public async Task GetTenants_AllowlistedEmail_SeesEveryClinicWithUsage()
    {
        var clinicA = await _db.SeedTenantAsync("Smile Dental", "a@clinic.com");
        await _db.SeedTenantAsync("City Care", "b@clinic.com");
        ActAsPlatformOwner();

        var tenants = await CreatePlatformService().GetTenantsAsync();

        Assert.Equal(2, tenants.Count);
        var smile = tenants.Single(t => t.Name == "Smile Dental");
        Assert.Equal(1, smile.StaffCount);         // the seeded admin
        Assert.Equal(0, smile.PatientCount);
        Assert.True(smile.IsActive);
        Assert.Equal(clinicA.TenantId, smile.TenantId);
        // Payment collection needs a person to call — the founding admin
        Assert.Equal("a@clinic.com", smile.OwnerEmail);
        Assert.Equal("Test Admin", smile.OwnerName);
    }

    [Fact]
    public async Task GetTenants_ClinicAdminEmail_IsRejected()
    {
        // A clinic Admin is NOT a platform admin — roles don't apply here
        var clinic = await _db.SeedTenantAsync("Smile Dental", "admin@clinic.com");
        _db.CurrentUser.ActAs(clinic.TenantId, clinic.TenantUserId);
        _db.CurrentUser.Email = "admin@clinic.com";

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => CreatePlatformService().GetTenantsAsync());
    }

    [Fact]
    public async Task ChangePlan_AppliedAfterManualPayment_UpdatesTenant()
    {
        var clinic = await _db.SeedTenantAsync("Smile Dental", "a@clinic.com");
        ActAsPlatformOwner();

        var updated = await CreatePlatformService().ChangePlanAsync(
            clinic.TenantId, new PlatformChangePlanRequest { Plan = "Growth" });

        Assert.Equal("Growth", updated.Plan);
    }

    [Fact]
    public async Task ChangePlan_UnknownPlan_IsRejected()
    {
        var clinic = await _db.SeedTenantAsync("Smile Dental", "a@clinic.com");
        ActAsPlatformOwner();

        await Assert.ThrowsAsync<BadRequestException>(
            () => CreatePlatformService().ChangePlanAsync(
                clinic.TenantId, new PlatformChangePlanRequest { Plan = "99" }));
    }

    [Fact]
    public async Task Suspend_BlocksSignInForTheWholeClinic()
    {
        await _db.SeedTenantAsync("Smile Dental", "doctor@clinic.com");
        var auth = CreateAuthService();

        // Works before suspension
        var login = new LoginRequest { Email = "doctor@clinic.com", Password = "Test-Pass-123" };
        var session = await auth.LoginAsync(login);
        Assert.NotNull(session.AccessToken);

        ActAsPlatformOwner();
        var suspended = await CreatePlatformService().SetActiveAsync(
            session.TenantId, new PlatformSetActiveRequest { IsActive = false });
        Assert.False(suspended.IsActive);

        // Next sign-in has no active clinic left → locked out
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => auth.LoginAsync(login));

        // Re-activation restores access — suspension never deletes anything
        ActAsPlatformOwner();
        await CreatePlatformService().SetActiveAsync(
            session.TenantId, new PlatformSetActiveRequest { IsActive = true });
        var restored = await auth.LoginAsync(login);
        Assert.Equal(session.TenantId, restored.TenantId);
    }

    [Fact]
    public async Task RecordPayment_ExtendsCoverage_AndNotifiesTheClinic()
    {
        var clinic = await _db.SeedTenantAsync("Smile Dental", "admin@clinic.com");
        // Realistic production shape: the platform owner is signed into their
        // OWN clinic while writing into the customer's tenant
        var ownClinic = await _db.SeedTenantAsync("Klivia HQ", "hq@klivia.com");
        _db.CurrentUser.ActAs(ownClinic.TenantId, ownClinic.TenantUserId);
        _db.CurrentUser.Email = OwnerEmail;

        var afterFirst = await CreatePlatformService().RecordPaymentAsync(
            clinic.TenantId, new RecordPaymentRequest
            {
                AmountRupees = 1999, Method = "Upi", PeriodMonths = 1, PlanToApply = "Clinic",
            });

        Assert.NotNull(afterFirst.PaidUntil);
        Assert.Equal("Clinic", afterFirst.Plan);
        Assert.Equal(1999, afterFirst.LastPaymentAmount);
        Assert.False(afterFirst.PaymentOverdue);
        // ~1 month out (paying now, no prior coverage)
        Assert.InRange(afterFirst.PaidUntil!.Value,
            DateTime.UtcNow.AddDays(27), DateTime.UtcNow.AddDays(32));

        // Second payment EXTENDS from the current coverage end, not from today
        var afterSecond = await CreatePlatformService().RecordPaymentAsync(
            clinic.TenantId, new RecordPaymentRequest
            {
                AmountRupees = 1999, Method = "Cash", PeriodMonths = 1,
            });
        Assert.True(afterSecond.PaidUntil > afterFirst.PaidUntil);

        // The clinic's Admin got thanked in-app (Billing notifications)
        await using var context = _db.CreateContext();
        _db.CurrentUser.ActAs(clinic.TenantId, clinic.TenantUserId);
        var notes = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions
            .ToListAsync(context.Notifications);
        Assert.Contains(notes, n => n.Type == "Billing" && n.Title.Contains("Payment received"));
    }

    [Fact]
    public async Task RecordPayment_BackdatedToTheRealPaymentDay_CoversFromThatDay()
    {
        // Owner records on Monday a UPI that landed on Saturday — coverage
        // must run from Saturday, and the history must show Saturday
        var clinic = await _db.SeedTenantAsync("Smile Dental", "admin@clinic.com");
        ActAsPlatformOwner();
        var saturday = DateTime.UtcNow.AddDays(-2);

        var updated = await CreatePlatformService().RecordPaymentAsync(
            clinic.TenantId, new RecordPaymentRequest
            {
                AmountRupees = 999, Method = "BankTransfer", PeriodMonths = 1, PaidAt = saturday,
            });

        Assert.InRange(updated.PaidUntil!.Value,
            saturday.AddDays(27), saturday.AddDays(32));

        var history = await CreatePlatformService().GetPaymentsAsync(clinic.TenantId);
        var payment = Assert.Single(history);
        Assert.Equal(saturday, payment.PaidAt, TimeSpan.FromSeconds(5));
        Assert.Equal("BankTransfer", payment.Method);

        // Future-dated payments are nonsense — refuse them
        await Assert.ThrowsAsync<BadRequestException>(
            () => CreatePlatformService().RecordPaymentAsync(clinic.TenantId,
                new RecordPaymentRequest
                {
                    AmountRupees = 999, Method = "Upi", PaidAt = DateTime.UtcNow.AddDays(9),
                }));
    }

    [Fact]
    public async Task RecordPayment_Garbage_IsRejected()
    {
        var clinic = await _db.SeedTenantAsync("Smile Dental", "admin@clinic.com");
        ActAsPlatformOwner();
        var service = CreatePlatformService();

        await Assert.ThrowsAsync<BadRequestException>(() => service.RecordPaymentAsync(
            clinic.TenantId, new RecordPaymentRequest { AmountRupees = 0, Method = "Upi" }));
        await Assert.ThrowsAsync<BadRequestException>(() => service.RecordPaymentAsync(
            clinic.TenantId, new RecordPaymentRequest { AmountRupees = 999, Method = "Hawala" }));
        await Assert.ThrowsAsync<BadRequestException>(() => service.RecordPaymentAsync(
            clinic.TenantId, new RecordPaymentRequest { AmountRupees = 999, Method = "Upi", PeriodMonths = 99 }));
    }

    [Fact]
    public async Task ChangePlan_NotifiesTheClinicAdmins()
    {
        var clinic = await _db.SeedTenantAsync("Smile Dental", "admin@clinic.com");
        ActAsPlatformOwner();

        await CreatePlatformService().ChangePlanAsync(
            clinic.TenantId, new PlatformChangePlanRequest { Plan = "Growth" });

        await using var context = _db.CreateContext();
        _db.CurrentUser.ActAs(clinic.TenantId, clinic.TenantUserId);
        var notes = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions
            .ToListAsync(context.Notifications);
        Assert.Contains(notes, n => n.Type == "Billing" && n.Title.Contains("Growth"));
    }

    [Fact]
    public async Task TestEmail_GoesToThePlatformAdmin_AndReportsOutcome()
    {
        ActAsPlatformOwner();

        var result = await CreatePlatformService().SendTestEmailAsync();

        Assert.True(result.Sent);
        Assert.Equal(OwnerEmail, result.To);
        Assert.Contains(_emails.Sent, m => m.To == OwnerEmail);
    }

    [Fact]
    public async Task AuthResponse_FlagsPlatformAdmin_OnlyForAllowlistedEmail()
    {
        var auth = CreateAuthService(platformAdmin: "owner@klivia.com");

        var owner = await auth.RegisterAsync(new RegisterRequest
        {
            FirstName = "Akash", LastName = "Owner",
            Email = "owner@klivia.com", Password = "Str0ng-Pass-123",
            ClinicName = "Klivia HQ"
        });
        var customer = await auth.RegisterAsync(new RegisterRequest
        {
            FirstName = "Priya", LastName = "Menon",
            Email = "priya@clinic.com", Password = "Str0ng-Pass-123",
            ClinicName = "Menon Dental"
        });

        Assert.True(owner.IsPlatformAdmin);
        Assert.False(customer.IsPlatformAdmin);
    }

    public void Dispose() => _db.Dispose();
}
