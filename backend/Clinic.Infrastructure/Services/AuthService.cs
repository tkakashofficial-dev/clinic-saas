using Clinic.Application.Common.Exceptions;
using Clinic.Application.Features.Auth.DTOs;
using Clinic.Application.Features.Auth.Services;
using Clinic.Domain.Constants;
using Clinic.Domain.Entities;
using Clinic.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text;

namespace Clinic.Infrastructure.Services;

public class AuthService : IAuthService
{
    private const int RefreshTokenLifetimeDays = 7;

    private readonly ClinicDbContext _context;
    private readonly JwtTokenGenerator _jwtTokenGenerator;

    public AuthService(ClinicDbContext context, JwtTokenGenerator jwtTokenGenerator)
    {
        _context = context;
        _jwtTokenGenerator = jwtTokenGenerator;
    }

    public async Task<AuthResponse> RegisterAsync(
        RegisterRequest request,
        CancellationToken cancellationToken = default)
    {
        // 1. Check email not already taken
        var emailExists = await _context.SystemUsers
            .AnyAsync(u => u.Email == request.Email, cancellationToken);

        if (emailExists)
            throw new ConflictException("Email is already registered.");

        // 2. Create SystemUser — the global login account
        var passwordHash = BCrypt.Net.BCrypt.HashPassword(request.Password);
        var systemUser = new SystemUser(
            request.Email,
            passwordHash,
            request.FirstName,
            request.LastName);

        _context.SystemUsers.Add(systemUser);

        // 3. Create Tenant — the clinic
        var tenant = new Tenant(request.ClinicName);
        _context.Tenants.Add(tenant);

        // 4. Create TenantUser — links this person to this clinic
        var tenantUser = new TenantUser(tenant.Id, systemUser.Id);
        _context.TenantUsers.Add(tenantUser);

        // 5. Seed the full closed set of roles for this tenant so
        //    [Authorize(Roles = ...)] can always match, then keep the Admin one
        Role adminRole = default!;
        foreach (var roleName in RoleNames.All)
        {
            var role = new Role(tenant.Id, roleName, RoleNames.DescriptionOf(roleName));
            _context.Roles.Add(role);
            if (roleName == RoleNames.Admin)
                adminRole = role;
        }

        // 6. Assign Admin role to this TenantUser
        var tenantUserRole = new TenantUserRole(tenantUser.Id, adminRole.Id);
        _context.TenantUserRoles.Add(tenantUserRole);

        // 7. Issue the refresh token alongside the account
        var refreshToken = IssueRefreshToken(systemUser.Id);

        // 8. Save everything in one transaction
        await _context.SaveChangesAsync(cancellationToken);

        var fullName = $"{request.FirstName} {request.LastName}";
        return BuildResponse(
            systemUser.Id, tenantUser.Id, tenant.Id,
            systemUser.Email, fullName, RoleNames.Admin, refreshToken);
    }

    public async Task<AuthResponse> LoginAsync(
        LoginRequest request,
        CancellationToken cancellationToken = default)
    {
        // 1. Find SystemUser by email
        var systemUser = await _context.SystemUsers
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Email == request.Email && u.IsActive, cancellationToken);

        if (systemUser is null)
            throw new UnauthorizedAccessException("Invalid email or password.");

        // 2. Verify password
        var passwordValid = BCrypt.Net.BCrypt.Verify(request.Password, systemUser.PasswordHash);
        if (!passwordValid)
            throw new UnauthorizedAccessException("Invalid email or password.");

        // 3. Resolve membership + role, issue tokens
        var (tenantUser, role) = await ResolveMembershipAsync(systemUser.Id, cancellationToken);

        var refreshToken = IssueRefreshToken(systemUser.Id);
        await _context.SaveChangesAsync(cancellationToken);

        var fullName = $"{systemUser.FirstName} {systemUser.LastName}";
        return BuildResponse(
            systemUser.Id, tenantUser.Id, tenantUser.TenantId,
            systemUser.Email, fullName, role, refreshToken);
    }

    public async Task<AuthResponse> RefreshAsync(
        RefreshRequest request,
        CancellationToken cancellationToken = default)
    {
        var tokenHash = HashToken(request.RefreshToken);

        var storedToken = await _context.RefreshTokens
            .Include(t => t.SystemUser)
            .FirstOrDefaultAsync(t => t.TokenHash == tokenHash, cancellationToken);

        if (storedToken is null || !storedToken.IsActive || !storedToken.SystemUser.IsActive)
            throw new UnauthorizedAccessException("Invalid or expired refresh token.");

        // Rotation: every refresh token is single-use. A replayed (stolen)
        // token is dead the moment the legitimate client has refreshed.
        storedToken.Revoke();

        var systemUser = storedToken.SystemUser;
        var (tenantUser, role) = await ResolveMembershipAsync(systemUser.Id, cancellationToken);

        var newRefreshToken = IssueRefreshToken(systemUser.Id);
        await _context.SaveChangesAsync(cancellationToken);

        var fullName = $"{systemUser.FirstName} {systemUser.LastName}";
        return BuildResponse(
            systemUser.Id, tenantUser.Id, tenantUser.TenantId,
            systemUser.Email, fullName, role, newRefreshToken);
    }

    /// <summary>
    /// Finds the user's active clinic membership and primary role.
    /// No JWT exists yet in these flows, so the tenant filter is skipped
    /// (soft-delete filtering stays active).
    /// </summary>
    private async Task<(TenantUser TenantUser, string Role)> ResolveMembershipAsync(
        Guid systemUserId,
        CancellationToken cancellationToken)
    {
        var tenantUser = await _context.TenantUsers
            .AsNoTracking()
            .IgnoreQueryFilters([QueryFilters.Tenant])
            .Include(tu => tu.Roles)
                .ThenInclude(r => r.Role)
            .FirstOrDefaultAsync(tu => tu.SystemUserId == systemUserId && tu.IsActive, cancellationToken);

        if (tenantUser is null)
            throw new UnauthorizedAccessException("No active clinic membership found.");

        var role = tenantUser.Roles
            .Select(r => r.Role.Name)
            .FirstOrDefault() ?? "Staff";

        return (tenantUser, role);
    }

    /// <summary>
    /// Creates a refresh token, stores its SHA-256 hash, and returns the RAW
    /// token (which is never persisted). Caller is responsible for SaveChanges.
    /// </summary>
    private string IssueRefreshToken(Guid systemUserId)
    {
        var rawToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
        _context.RefreshTokens.Add(new RefreshToken(
            systemUserId,
            HashToken(rawToken),
            DateTime.UtcNow.AddDays(RefreshTokenLifetimeDays)));
        return rawToken;
    }

    private static string HashToken(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    private AuthResponse BuildResponse(
        Guid systemUserId, Guid tenantUserId, Guid tenantId,
        string email, string fullName, string role, string refreshToken)
    {
        var (token, expiresAt) = _jwtTokenGenerator.Generate(
            systemUserId, tenantUserId, tenantId, email, fullName, role);

        return new AuthResponse
        {
            AccessToken = token,
            RefreshToken = refreshToken,
            Email = email,
            FullName = fullName,
            Role = role,
            TenantId = tenantId,
            TenantUserId = tenantUserId,
            ExpiresAt = expiresAt
        };
    }
}
