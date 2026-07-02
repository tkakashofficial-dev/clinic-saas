using Clinic.Application.Common.Exceptions;
using Clinic.Application.Features.Auth.DTOs;
using Clinic.Domain.Constants;
using Clinic.Infrastructure.Services;
using Clinic.Tests.TestInfrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Clinic.Tests;

public class AuthServiceTests : IDisposable
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

    private static RegisterRequest ValidRegistration(string email = "owner@clinic.com") => new()
    {
        FirstName = "Owner",
        LastName = "Person",
        Email = email,
        Password = "Str0ng-Pass-123",
        ClinicName = "Bright Smiles"
    };

    [Fact]
    public async Task Register_OwnerIsDoctor_GetsBothRoles()
    {
        // The common Kerala case: the clinic owner is the practicing dentist
        var request = ValidRegistration("drowner@clinic.com");
        request.OwnerIsDoctor = true;

        var response = await CreateService().RegisterAsync(request);

        Assert.Equal(RoleNames.Admin, response.Role); // primary stays Admin
        Assert.Contains(RoleNames.Doctor, response.Roles);
        Assert.Equal(2, response.Roles.Count);
    }

    [Fact]
    public async Task Register_CreatesTenant_SeedsAllRoles_AssignsAdmin()
    {
        var response = await CreateService().RegisterAsync(ValidRegistration());

        Assert.Equal(RoleNames.Admin, response.Role);
        Assert.Equal([RoleNames.Admin], response.Roles); // investor-owner: Admin only
        Assert.NotEqual(Guid.Empty, response.TenantId);
        Assert.False(string.IsNullOrWhiteSpace(response.AccessToken));

        // All three roles must exist for the new tenant (so staff can be added)
        _db.CurrentUser.ActAs(response.TenantId, response.TenantUserId);
        await using var context = _db.CreateContext();
        var roleNames = await context.Roles.Select(r => r.Name).ToListAsync();
        Assert.Equal(RoleNames.All.Order(), roleNames.Order());
    }

    [Fact]
    public async Task Register_DuplicateEmail_ThrowsConflict()
    {
        await CreateService().RegisterAsync(ValidRegistration("dup@clinic.com"));

        await Assert.ThrowsAsync<ConflictException>(
            () => CreateService().RegisterAsync(ValidRegistration("dup@clinic.com")));
    }

    [Fact]
    public async Task Login_CorrectPassword_ReturnsTokenWithTenant()
    {
        var registered = await CreateService().RegisterAsync(ValidRegistration("login@clinic.com"));

        // Login happens with NO current tenant — exercises the named
        // IgnoreQueryFilters(Tenant) path
        _db.CurrentUser.ActAsAnonymous();
        var response = await CreateService().LoginAsync(new LoginRequest
        {
            Email = "login@clinic.com",
            Password = "Str0ng-Pass-123"
        });

        Assert.Equal(registered.TenantId, response.TenantId);
        Assert.Equal(RoleNames.Admin, response.Role);
    }

    [Fact]
    public async Task Login_WrongPassword_ThrowsUnauthorized()
    {
        await CreateService().RegisterAsync(ValidRegistration("wrongpw@clinic.com"));

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => CreateService().LoginAsync(new LoginRequest
            {
                Email = "wrongpw@clinic.com",
                Password = "not-the-password"
            }));
    }

    [Fact]
    public async Task Login_UnknownEmail_ThrowsUnauthorized()
    {
        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => CreateService().LoginAsync(new LoginRequest
            {
                Email = "ghost@clinic.com",
                Password = "whatever-123"
            }));
    }

    public void Dispose() => _db.Dispose();
}
