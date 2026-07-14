using Clinic.Application.Features.Auth.DTOs;
using Clinic.Infrastructure.Services;
using Clinic.Tests.TestInfrastructure;
using Microsoft.Extensions.Options;

namespace Clinic.Tests;

/// <summary>
/// Multi-clinic owners: one login, several clinics, strict membership checks.
/// </summary>
public class MultiClinicTests : IDisposable
{
    private readonly TestDb _db = new();

    private AuthService CreateService() =>
        new(_db.CreateContext(), new JwtTokenGenerator(Options.Create(new JwtSettings
        {
            Secret = new string('k', 64),
            Issuer = "TestIssuer",
            Audience = "TestAudience",
            ExpiresInMinutes = 60
        })), new NoOpEmailSender(), Options.Create(new FrontendSettings()), Options.Create(new PlatformSettings()));

    private async Task<AuthResponse> RegisterAsync(string email) =>
        await CreateService().RegisterAsync(new RegisterRequest
        {
            FirstName = "Multi",
            LastName = "Owner",
            Email = email,
            Password = "Str0ng-Pass-123",
            ClinicName = "First Clinic",
            OwnerIsDoctor = true
        });

    [Fact]
    public async Task CreateClinic_AddsSecondMembership_AndLandsInNewClinic()
    {
        var first = await RegisterAsync("owner1@clinics.com");

        var second = await CreateService().CreateClinicAsync(
            GetSystemUserId(first), new CreateClinicRequest { Name = "Second Clinic", OwnerIsDoctor = false });

        Assert.NotEqual(first.TenantId, second.TenantId);
        Assert.Equal("Second Clinic", second.ClinicName);
        Assert.Equal(2, second.Memberships.Count);
        Assert.Equal("Admin", second.Role); // owner of the new clinic
    }

    [Fact]
    public async Task CreateClinic_WhileSignedIntoAnotherClinic_Succeeds()
    {
        // REGRESSION (user-reported 500): in production the caller's token is
        // scoped to their CURRENT clinic, so provisioning the new clinic's
        // TenantUser/Roles tripped the cross-tenant write guard. Reproduce
        // that by acting as the signed-in user — not anonymously.
        var first = await RegisterAsync("owner5@clinics.com");
        _db.CurrentUser.ActAs(first.TenantId, first.TenantUserId, GetSystemUserId(first));

        var second = await CreateService().CreateClinicAsync(
            GetSystemUserId(first), new CreateClinicRequest { Name = "Branch Two", OwnerIsDoctor = true });

        Assert.Equal("Branch Two", second.ClinicName);
        Assert.Equal(2, second.Memberships.Count);
    }

    [Fact]
    public async Task CrossTenantWrite_IntoExistingForeignClinic_StaysBlocked()
    {
        // The provisioning whitelist must NOT weaken the guard for tenants
        // that already exist: clinic A writing into clinic B still throws.
        var mine = await RegisterAsync("owner6@clinics.com");
        var theirs = await CreateService().RegisterAsync(new RegisterRequest
        {
            FirstName = "Other", LastName = "Owner", Email = "other6@clinics.com",
            Password = "Str0ng-Pass-123", ClinicName = "Their Clinic"
        });
        _db.CurrentUser.ActAs(mine.TenantId, mine.TenantUserId, GetSystemUserId(mine));

        await using var context = _db.CreateContext();
        context.InventoryItems.Add(new Clinic.Domain.Entities.InventoryItem(
            theirs.TenantId, "Smuggled item", Clinic.Domain.Enums.InventoryCategory.Medicine,
            "piece", 1, 0));

        await Assert.ThrowsAsync<InvalidOperationException>(() => context.SaveChangesAsync());
    }

    [Fact]
    public async Task SwitchClinic_BetweenOwnClinics_Works()
    {
        var first = await RegisterAsync("owner2@clinics.com");
        var systemUserId = GetSystemUserId(first);
        await CreateService().CreateClinicAsync(
            systemUserId, new CreateClinicRequest { Name = "Second Clinic" });

        var switched = await CreateService().SwitchClinicAsync(
            systemUserId, new SwitchClinicRequest { TenantId = first.TenantId });

        Assert.Equal(first.TenantId, switched.TenantId);
        Assert.Equal("First Clinic", switched.ClinicName);
    }

    [Fact]
    public async Task SwitchClinic_ToSomeoneElsesClinic_IsRejected()
    {
        var mine = await RegisterAsync("owner3@clinics.com");
        var theirs = await CreateService().RegisterAsync(new RegisterRequest
        {
            FirstName = "Other",
            LastName = "Owner",
            Email = "other@clinics.com",
            Password = "Str0ng-Pass-123",
            ClinicName = "Their Clinic"
        });

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => CreateService().SwitchClinicAsync(
                GetSystemUserId(mine), new SwitchClinicRequest { TenantId = theirs.TenantId }));
    }

    [Fact]
    public async Task Login_MultiClinicOwner_ReturnsAllMemberships()
    {
        var first = await RegisterAsync("owner4@clinics.com");
        await CreateService().CreateClinicAsync(
            GetSystemUserId(first), new CreateClinicRequest { Name = "Second Clinic" });

        var login = await CreateService().LoginAsync(new LoginRequest
        {
            Email = "owner4@clinics.com",
            Password = "Str0ng-Pass-123"
        });

        Assert.Equal(2, login.Memberships.Count);
        Assert.Contains(login.Memberships, m => m.ClinicName == "First Clinic");
        Assert.Contains(login.Memberships, m => m.ClinicName == "Second Clinic");
    }

    private static Guid GetSystemUserId(AuthResponse response)
    {
        // The JWT nameid claim carries the SystemUserId; decode the payload
        var payload = response.AccessToken.Split('.')[1];
        var padded = payload.PadRight(payload.Length + (4 - payload.Length % 4) % 4, '=')
            .Replace('-', '+').Replace('_', '/');
        var json = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(padded));
        var start = json.IndexOf("nameidentifier\":\"", StringComparison.Ordinal) + 17;
        return Guid.Parse(json.Substring(start, 36));
    }

    public void Dispose() => _db.Dispose();
}
