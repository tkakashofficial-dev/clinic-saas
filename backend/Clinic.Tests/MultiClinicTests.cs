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
        })), new NoOpEmailSender());

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
