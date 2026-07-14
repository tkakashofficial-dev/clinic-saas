using Clinic.Application.Common.Exceptions;
using Clinic.Application.Common.Interfaces;
using Clinic.Application.Features.Auth.DTOs;
using Clinic.Application.Features.Auth.Services;
using Clinic.Domain.Constants;
using Clinic.Domain.Entities;
using Clinic.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Security.Cryptography;
using System.Text;

namespace Clinic.Infrastructure.Services;

/// <summary>Where the Angular app lives — used to build email links.</summary>
public class FrontendSettings
{
    public string BaseUrl { get; set; } = "http://localhost:4200";
}

public class AuthService : IAuthService
{
    private const int RefreshTokenLifetimeDays = 7;
    private const int ResetTokenLifetimeMinutes = 60;

    private readonly ClinicDbContext _context;
    private readonly JwtTokenGenerator _jwtTokenGenerator;
    private readonly IEmailSender _emailSender;
    private readonly FrontendSettings _frontend;
    private readonly PlatformSettings _platform;

    public AuthService(
        ClinicDbContext context,
        JwtTokenGenerator jwtTokenGenerator,
        IEmailSender emailSender,
        IOptions<FrontendSettings> frontendSettings,
        IOptions<PlatformSettings> platformSettings)
    {
        _context = context;
        _jwtTokenGenerator = jwtTokenGenerator;
        _emailSender = emailSender;
        _frontend = frontendSettings.Value;
        _platform = platformSettings.Value;
    }

    public async Task<AuthResponse> RegisterAsync(
        RegisterRequest request,
        CancellationToken cancellationToken = default)
    {
        var emailExists = await _context.SystemUsers
            .AnyAsync(u => u.Email == request.Email, cancellationToken);

        if (emailExists)
            throw new ConflictException("Email is already registered.");

        var passwordHash = BCrypt.Net.BCrypt.HashPassword(request.Password);
        var systemUser = new SystemUser(
            request.Email,
            passwordHash,
            request.FirstName,
            request.LastName);

        _context.SystemUsers.Add(systemUser);

        // Provision the clinic: tenant + membership + seeded roles + owner roles
        var (tenant, tenantUser, ownerRoles) =
            ProvisionClinic(systemUser.Id, request.ClinicName, request.OwnerIsDoctor);

        var refreshToken = IssueRefreshToken(systemUser.Id);
        await _context.SaveChangesAsync(cancellationToken);

        // Welcome email is intentionally asynchronous so mail delivery issues never
        // make signup feel stuck or slow for the clinic owner.
        _ = _emailSender.SendAsync(
            systemUser.Email,
            $"Welcome to Klivia — {tenant.Name} is ready",
            EmailTemplates.Welcome(
                request.FirstName,
                tenant.Name,
                $"{_frontend.BaseUrl.TrimEnd('/')}/dashboard"),
            CancellationToken.None);

        var fullName = $"{request.FirstName} {request.LastName}";
        return BuildResponse(
            systemUser.Id, tenantUser.Id, tenant.Id, tenant.Name,
            systemUser.Email, fullName, ownerRoles, refreshToken,
            [new MembershipDto { TenantId = tenant.Id, ClinicName = tenant.Name }]);
    }

    public async Task<AuthResponse> LoginAsync(
        LoginRequest request,
        CancellationToken cancellationToken = default)
    {
        var systemUser = await _context.SystemUsers
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Email == request.Email && u.IsActive, cancellationToken);

        if (systemUser is null)
            throw new UnauthorizedAccessException("Invalid email or password.");

        var passwordValid = BCrypt.Net.BCrypt.Verify(request.Password, systemUser.PasswordHash);
        if (!passwordValid)
            throw new UnauthorizedAccessException("Invalid email or password.");

        return await IssueForUserAsync(systemUser, preferredTenantId: null, cancellationToken);
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

        // Rotation: every refresh token is single-use
        storedToken.Revoke();

        return await IssueForUserAsync(storedToken.SystemUser, preferredTenantId: null, cancellationToken);
    }

    public async Task<AuthResponse> CreateClinicAsync(
        Guid systemUserId,
        CreateClinicRequest request,
        CancellationToken cancellationToken = default)
    {
        var systemUser = await _context.SystemUsers
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == systemUserId && u.IsActive, cancellationToken)
            ?? throw new UnauthorizedAccessException("User not found.");

        var (tenant, tenantUser, ownerRoles) =
            ProvisionClinic(systemUser.Id, request.Name, request.OwnerIsDoctor);

        // Caller's token is scoped to their CURRENT clinic; whitelist the
        // brand-new tenant id or the cross-tenant write guard rejects this
        _context.AllowProvisioningFor(tenant.Id);

        var refreshToken = IssueRefreshToken(systemUser.Id);
        await _context.SaveChangesAsync(cancellationToken);

        // Respond scoped to the NEW clinic so the UI lands there directly
        var memberships = await GetMembershipsAsync(systemUser.Id, cancellationToken);
        var fullName = $"{systemUser.FirstName} {systemUser.LastName}";
        return BuildResponse(
            systemUser.Id, tenantUser.Id, tenant.Id, tenant.Name,
            systemUser.Email, fullName, ownerRoles, refreshToken, memberships);
    }

    public async Task<AuthResponse> SwitchClinicAsync(
        Guid systemUserId,
        SwitchClinicRequest request,
        CancellationToken cancellationToken = default)
    {
        var systemUser = await _context.SystemUsers
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == systemUserId && u.IsActive, cancellationToken)
            ?? throw new UnauthorizedAccessException("User not found.");

        var response = await IssueForUserAsync(systemUser, request.TenantId, cancellationToken);

        // If the preferred tenant wasn't among the memberships, refuse loudly
        if (response.TenantId != request.TenantId)
            throw new UnauthorizedAccessException("You are not a member of that clinic.");

        return response;
    }

    public async Task ForgotPasswordAsync(
        ForgotPasswordRequest request,
        CancellationToken cancellationToken = default)
    {
        var systemUser = await _context.SystemUsers
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Email == request.Email && u.IsActive, cancellationToken);

        // SECURITY: identical outward behavior whether or not the account
        // exists — otherwise this endpoint becomes an email-enumeration oracle
        if (systemUser is null)
            return;

        var resetLink = await CreatePasswordLinkAsync(
            systemUser.Id, TimeSpan.FromMinutes(ResetTokenLifetimeMinutes), cancellationToken);

        await _emailSender.SendAsync(
            systemUser.Email,
            "Reset your Klivia password",
            EmailTemplates.PasswordReset(
                systemUser.FirstName, resetLink, ResetTokenLifetimeMinutes),
            cancellationToken);
    }

    public async Task ResetPasswordAsync(
        ResetPasswordRequest request,
        CancellationToken cancellationToken = default)
    {
        var tokenHash = HashToken(request.Token);

        var resetToken = await _context.PasswordResetTokens
            .Include(t => t.SystemUser)
            .FirstOrDefaultAsync(t => t.TokenHash == tokenHash, cancellationToken);

        if (resetToken is null || !resetToken.IsValid || !resetToken.SystemUser.IsActive)
            throw new UnauthorizedAccessException(
                "This reset link is invalid or has expired. Request a new one.");

        resetToken.MarkUsed();
        resetToken.SystemUser.UpdatePassword(BCrypt.Net.BCrypt.HashPassword(request.NewPassword));

        // A password change invalidates every existing session — if the reset
        // happened because of a stolen password, the thief is logged out too
        var activeRefreshTokens = await _context.RefreshTokens
            .Where(t => t.SystemUserId == resetToken.SystemUserId && t.RevokedAt == null)
            .ToListAsync(cancellationToken);
        foreach (var token in activeRefreshTokens)
            token.Revoke();

        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<InviteInfoDto> GetInviteInfoAsync(
        string token, CancellationToken cancellationToken = default)
    {
        var tokenHash = HashToken(token);

        var resetToken = await _context.PasswordResetTokens
            .AsNoTracking()
            .Include(t => t.SystemUser)
            .FirstOrDefaultAsync(t => t.TokenHash == tokenHash, cancellationToken);

        if (resetToken is null || !resetToken.IsValid || !resetToken.SystemUser.IsActive)
            throw new UnauthorizedAccessException(
                "This invite link is invalid or has expired. Ask your clinic admin to re-invite you.");

        var memberships = await GetMembershipsAsync(resetToken.SystemUserId, cancellationToken);

        return new InviteInfoDto
        {
            FirstName = resetToken.SystemUser.FirstName,
            Email = resetToken.SystemUser.Email,
            ClinicNames = memberships.Select(m => m.ClinicName).ToList()
        };
    }

    /// <summary>
    /// Creates a single-use password token and returns the full frontend link.
    /// Used by forgot-password (short expiry) and staff invites (long expiry).
    /// Saves changes.
    /// </summary>
    public async Task<string> CreatePasswordLinkAsync(
        Guid systemUserId, TimeSpan lifetime, CancellationToken cancellationToken)
    {
        var rawToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(48))
            .Replace('+', '-').Replace('/', '_').TrimEnd('=');   // URL-safe

        _context.PasswordResetTokens.Add(new PasswordResetToken(
            systemUserId, HashToken(rawToken), DateTime.UtcNow.Add(lifetime)));
        await _context.SaveChangesAsync(cancellationToken);

        return $"{_frontend.BaseUrl.TrimEnd('/')}/reset-password?token={rawToken}";
    }

    /// <summary>
    /// Creates a Tenant, links the user as its member, seeds the closed role
    /// set, and assigns owner roles (Admin, plus Doctor for practicing owners).
    /// Caller is responsible for SaveChanges.
    /// </summary>
    private (Tenant Tenant, TenantUser TenantUser, List<string> OwnerRoles) ProvisionClinic(
        Guid systemUserId, string clinicName, bool ownerIsDoctor)
    {
        var tenant = new Tenant(clinicName);
        _context.Tenants.Add(tenant);

        var tenantUser = new TenantUser(tenant.Id, systemUserId);
        _context.TenantUsers.Add(tenantUser);

        var seededRoles = new Dictionary<string, Role>();
        foreach (var roleName in RoleNames.All)
        {
            var role = new Role(tenant.Id, roleName, RoleNames.DescriptionOf(roleName));
            _context.Roles.Add(role);
            seededRoles[roleName] = role;
        }

        var ownerRoles = new List<string> { RoleNames.Admin };
        if (ownerIsDoctor)
            ownerRoles.Add(RoleNames.Doctor);

        foreach (var roleName in ownerRoles)
            _context.TenantUserRoles.Add(
                new TenantUserRole(tenantUser.Id, seededRoles[roleName].Id));

        return (tenant, tenantUser, ownerRoles);
    }

    /// <summary>
    /// Issues a full token pair for a user, scoped to the preferred clinic
    /// (or their first clinic when no preference). Saves changes.
    /// </summary>
    private async Task<AuthResponse> IssueForUserAsync(
        SystemUser systemUser, Guid? preferredTenantId, CancellationToken cancellationToken)
    {
        // No JWT exists yet in these flows, so the tenant filter is skipped
        // (soft-delete filtering stays active)
        var tenantUsers = await _context.TenantUsers
            .AsNoTracking()
            .IgnoreQueryFilters([QueryFilters.Tenant])
            .Include(tu => tu.Tenant)
            .Include(tu => tu.Roles)
                .ThenInclude(r => r.Role)
            // Suspended clinics (platform action, e.g. non-payment) stop
            // issuing sessions immediately
            .Where(tu => tu.SystemUserId == systemUser.Id && tu.IsActive && tu.Tenant.IsActive)
            .OrderBy(tu => tu.CreatedAt)
            .ToListAsync(cancellationToken);

        if (tenantUsers.Count == 0)
            throw new UnauthorizedAccessException("No active clinic membership found.");

        var current = tenantUsers.FirstOrDefault(tu => tu.TenantId == preferredTenantId)
                      ?? tenantUsers[0];

        var roles = current.Roles
            .Select(r => r.Role.Name)
            .OrderBy(name => RoleNames.All.ToList().IndexOf(name))
            .ToList();
        if (roles.Count == 0)
            roles.Add(RoleNames.Receptionist);

        var memberships = tenantUsers
            .Select(tu => new MembershipDto { TenantId = tu.TenantId, ClinicName = tu.Tenant.Name })
            .ToList();

        var refreshToken = IssueRefreshToken(systemUser.Id);
        await _context.SaveChangesAsync(cancellationToken);

        var fullName = $"{systemUser.FirstName} {systemUser.LastName}";
        return BuildResponse(
            systemUser.Id, current.Id, current.TenantId, current.Tenant.Name,
            systemUser.Email, fullName, roles, refreshToken, memberships);
    }

    private async Task<List<MembershipDto>> GetMembershipsAsync(
        Guid systemUserId, CancellationToken cancellationToken)
    {
        return await _context.TenantUsers
            .AsNoTracking()
            .IgnoreQueryFilters([QueryFilters.Tenant])
            // Same rule as IssueForUserAsync: suspended clinics never appear
            .Where(tu => tu.SystemUserId == systemUserId && tu.IsActive && tu.Tenant.IsActive)
            .OrderBy(tu => tu.CreatedAt)
            .Select(tu => new MembershipDto
            {
                TenantId = tu.TenantId,
                ClinicName = tu.Tenant.Name
            })
            .ToListAsync(cancellationToken);
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
        Guid systemUserId, Guid tenantUserId, Guid tenantId, string clinicName,
        string email, string fullName, List<string> roles, string refreshToken,
        List<MembershipDto> memberships)
    {
        var (token, expiresAt) = _jwtTokenGenerator.Generate(
            systemUserId, tenantUserId, tenantId, email, fullName, roles);

        return new AuthResponse
        {
            AccessToken = token,
            RefreshToken = refreshToken,
            Email = email,
            FullName = fullName,
            Role = roles[0],
            Roles = roles,
            TenantId = tenantId,
            TenantUserId = tenantUserId,
            ClinicName = clinicName,
            Memberships = memberships,
            IsPlatformAdmin = PlatformService.IsPlatformAdmin(_platform, email),
            ExpiresAt = expiresAt
        };
    }
}
