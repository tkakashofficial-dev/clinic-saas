using Clinic.Application.Features.Auth.DTOs;
using Clinic.Infrastructure.Services;
using Clinic.Tests.TestInfrastructure;
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
        })));

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
