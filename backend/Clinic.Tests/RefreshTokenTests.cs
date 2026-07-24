using Clinic.Application.Features.Auth.DTOs;
using Clinic.Infrastructure.Services;
using Clinic.Tests.TestInfrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Clinic.Tests;

public class RefreshTokenTests : IDisposable
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

    private async Task<AuthResponse> RegisterAsync(string email = "owner@clinic.com") =>
        await CreateService().RegisterAsync(new RegisterRequest
        {
            FirstName = "Owner",
            LastName = "Person",
            Email = email,
            Password = "Str0ng-Pass-123",
            ClinicName = "Bright Smiles"
        });

    [Fact]
    public async Task Login_ReturnsRefreshToken()
    {
        await RegisterAsync("rt1@clinic.com");
        var response = await CreateService().LoginAsync(new LoginRequest
        {
            Email = "rt1@clinic.com",
            Password = "Str0ng-Pass-123"
        });

        Assert.False(string.IsNullOrWhiteSpace(response.RefreshToken));
    }

    [Fact]
    public async Task Refresh_KeepsTheClinicTheUserWasWorkingIn()
    {
        // Multi-clinic owner working in clinic B: a silent token refresh
        // must NOT flip the session back to clinic A (their first clinic)
        var registered = await RegisterAsync("rt-multi@clinic.com");

        // Multi-clinic needs Growth
        await using (var context = _db.CreateContext())
        {
            var tenant = await context.Tenants.FirstAsync(t => t.Id == registered.TenantId);
            tenant.ChangePlan(Clinic.Domain.Enums.PlanType.Growth);
            await context.SaveChangesAsync();
        }

        var systemUserId = GetSystemUserId(registered);
        _db.CurrentUser.ActAs(registered.TenantId, registered.TenantUserId, systemUserId);

        var clinicB = await CreateService().CreateClinicAsync(
            systemUserId,
            new CreateClinicRequest { Name = "Second Branch", OwnerIsDoctor = false });
        Assert.NotEqual(registered.TenantId, clinicB.TenantId);

        var refreshed = await CreateService().RefreshAsync(new RefreshRequest
        {
            RefreshToken = clinicB.RefreshToken,
            TenantId = clinicB.TenantId,
        });

        Assert.Equal(clinicB.TenantId, refreshed.TenantId);
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

    [Fact]
    public async Task Refresh_ValidToken_ReturnsNewPair()
    {
        var registered = await RegisterAsync("rt2@clinic.com");

        var refreshed = await CreateService().RefreshAsync(new RefreshRequest
        {
            RefreshToken = registered.RefreshToken
        });

        Assert.Equal(registered.TenantId, refreshed.TenantId);
        Assert.NotEqual(registered.RefreshToken, refreshed.RefreshToken); // rotated
        Assert.False(string.IsNullOrWhiteSpace(refreshed.AccessToken));
    }

    [Fact]
    public async Task Refresh_ReusedToken_IsRejected()
    {
        // Rotation security: once used, a refresh token is dead —
        // a replayed (stolen) token must fail
        var registered = await RegisterAsync("rt3@clinic.com");

        await CreateService().RefreshAsync(new RefreshRequest
        {
            RefreshToken = registered.RefreshToken
        });

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => CreateService().RefreshAsync(new RefreshRequest
            {
                RefreshToken = registered.RefreshToken
            }));
    }

    [Fact]
    public async Task InviteInfo_ValidInviteToken_ReturnsWhoAndWhere()
    {
        // Owner invites a doctor (invite-only) — the emailed token must
        // resolve to "who is joining which clinic" for the accept page
        var owner = await RegisterAsync("inviteinfo@clinic.com");

        var emails = new NoOpEmailSender();
        var staffService = new StaffService(
            _db.CreateContext(), _db.CurrentUser, emails,
            Options.Create(new FrontendSettings()));
        _db.CurrentUser.ActAs(owner.TenantId, owner.TenantUserId);

        await staffService.AddStaffAsync(new Clinic.Application.Features.Staff.DTOs.AddStaffRequest
        {
            FirstName = "Invited",
            LastName = "Doctor",
            Email = "invited@clinic.com",
            Password = null,
            Roles = ["Doctor"],
        });

        // Extract the raw token from the emailed accept-invite link
        var body = emails.Sent.Single().Body;
        var marker = "accept-invite?token=";
        var start = body.IndexOf(marker, StringComparison.Ordinal) + marker.Length;
        var token = body[start..body.IndexOfAny(['"', '<', '&'], start)];

        var info = await CreateService().GetInviteInfoAsync(token);

        Assert.Equal("Invited", info.FirstName);
        Assert.Equal("invited@clinic.com", info.Email);
        Assert.Contains("Bright Smiles", info.ClinicNames);
    }

    [Fact]
    public async Task InviteInfo_GarbageToken_IsRejected()
    {
        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => CreateService().GetInviteInfoAsync("not-a-token"));
    }

    [Fact]
    public async Task Refresh_GarbageToken_IsRejected()
    {
        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => CreateService().RefreshAsync(new RefreshRequest
            {
                RefreshToken = "not-a-real-token"
            }));
    }

    public void Dispose() => _db.Dispose();
}
