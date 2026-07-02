using Clinic.Application.Features.Auth.DTOs;
using Clinic.Application.Features.Auth.Services;
using Clinic.Domain.Entities;
using Clinic.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Clinic.Infrastructure.Services;

public class AuthService : IAuthService
{
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
            throw new InvalidOperationException("Email is already registered.");

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

        // 5. Create the Admin role for this tenant
        var adminRole = new Role(tenant.Id, "Admin", "Clinic administrator with full access");
        _context.Roles.Add(adminRole);

        // 6. Assign Admin role to this TenantUser
        var tenantUserRole = new TenantUserRole(tenantUser.Id, adminRole.Id);
        _context.TenantUserRoles.Add(tenantUserRole);

        // 7. Save everything in one transaction
        await _context.SaveChangesAsync(cancellationToken);

        // 8. Generate JWT token
        var fullName = $"{request.FirstName} {request.LastName}";
        var (token, expiresAt) = _jwtTokenGenerator.Generate(
            systemUser.Id,
            tenantUser.Id,
            tenant.Id,
            systemUser.Email,
            fullName,
            "Admin");

        return new AuthResponse
        {
            AccessToken = token,
            Email = systemUser.Email,
            FullName = fullName,
            Role = "Admin",
            TenantId = tenant.Id,
            TenantUserId = tenantUser.Id,
            ExpiresAt = expiresAt
        };
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

        // 3. Get TenantUser with roles.
        // No JWT exists yet at login, so no "current tenant" — skip ONLY the tenant
        // filter (soft-delete filtering stays active).
        var tenantUser = await _context.TenantUsers
            .AsNoTracking()
            .IgnoreQueryFilters([QueryFilters.Tenant])
            .Include(tu => tu.Roles)
                .ThenInclude(r => r.Role)
            .FirstOrDefaultAsync(tu => tu.SystemUserId == systemUser.Id && tu.IsActive, cancellationToken);

        if (tenantUser is null)
            throw new UnauthorizedAccessException("No active clinic membership found.");

        // 4. Get primary role
        var role = tenantUser.Roles
            .Select(r => r.Role.Name)
            .FirstOrDefault() ?? "Staff";

        // 5. Generate token
        var fullName = $"{systemUser.FirstName} {systemUser.LastName}";
        var (token, expiresAt) = _jwtTokenGenerator.Generate(
            systemUser.Id,
            tenantUser.Id,
            tenantUser.TenantId,
            systemUser.Email,
            fullName,
            role);

        return new AuthResponse
        {
            AccessToken = token,
            Email = systemUser.Email,
            FullName = fullName,
            Role = role,
            TenantId = tenantUser.TenantId,
            TenantUserId = tenantUser.Id,
            ExpiresAt = expiresAt
        };
    }
}