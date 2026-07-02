using Clinic.Application.Common.Exceptions;
using Clinic.Application.Common.Interfaces;
using Clinic.Application.Common.Models;
using Clinic.Application.Features.Staff.DTOs;
using Clinic.Application.Features.Staff.Services;
using Clinic.Domain.Constants;
using Clinic.Domain.Entities;
using Clinic.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Clinic.Infrastructure.Services;

public class StaffService : IStaffService
{
    private readonly ClinicDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly IEmailSender _emailSender;
    private readonly FrontendSettings _frontend;

    public StaffService(
        ClinicDbContext context,
        ICurrentUserService currentUser,
        IEmailSender emailSender,
        IOptions<FrontendSettings> frontendSettings)
    {
        _context = context;
        _currentUser = currentUser;
        _emailSender = emailSender;
        _frontend = frontendSettings.Value;
    }

    public async Task<StaffDto> AddStaffAsync(
        AddStaffRequest request,
        CancellationToken cancellationToken = default)
    {
        // TenantId comes from JWT token — never from request body
        var tenantId = _currentUser.TenantId;

        // 0. Roles are a closed set — free-text would silently break
        //    [Authorize(Roles = ...)] matching (e.g. a typo like "Docter").
        //    Multiple roles combine: a partner who practices = Admin + Doctor.
        var roleNames = new List<string>();
        foreach (var requested in request.Roles.Distinct())
        {
            if (!RoleNames.TryNormalize(requested, out var normalized))
                throw new BadRequestException(
                    $"Invalid role '{requested}'. Allowed roles: {string.Join(", ", RoleNames.All)}.");
            if (!roleNames.Contains(normalized))
                roleNames.Add(normalized);
        }

        if (roleNames.Count == 0)
            throw new BadRequestException("At least one role is required.");

        // 1. Check email not already taken globally
        var emailExists = await _context.SystemUsers
            .AnyAsync(u => u.Email == request.Email, cancellationToken);

        if (emailExists)
            throw new ConflictException("Email is already registered.");

        // 2. Create SystemUser
        var passwordHash = BCrypt.Net.BCrypt.HashPassword(request.Password);
        var systemUser = new SystemUser(
            request.Email,
            passwordHash,
            request.FirstName,
            request.LastName);

        _context.SystemUsers.Add(systemUser);

        // 3. Create TenantUser — link to this clinic
        var tenantUser = new TenantUser(tenantId, systemUser.Id);
        _context.TenantUsers.Add(tenantUser);

        // 4. Assign every requested role (seeded roles; create as fallback
        //    for tenants registered before role seeding existed)
        foreach (var roleName in roleNames)
        {
            var role = await _context.Roles
                .FirstOrDefaultAsync(r => r.TenantId == tenantId
                    && r.Name == roleName, cancellationToken);

            if (role is null)
            {
                role = new Role(tenantId, roleName, RoleNames.DescriptionOf(roleName));
                _context.Roles.Add(role);
            }

            _context.TenantUserRoles.Add(new TenantUserRole(tenantUser.Id, role.Id));
        }

        // 5. Invite link: staff should choose their OWN password on first
        //    login — the admin's temporary one keeps working meanwhile
        var inviteToken = SecurityTokens.CreateUrlSafe();
        _context.PasswordResetTokens.Add(new PasswordResetToken(
            systemUser.Id,
            SecurityTokens.Sha256Hex(inviteToken),
            DateTime.UtcNow.AddDays(7)));

        // 6. Save everything
        await _context.SaveChangesAsync(cancellationToken);

        // 7. Welcome/invite email (failure-proof — never blocks staff creation)
        var clinicName = await _context.Tenants
            .Where(t => t.Id == tenantId)
            .Select(t => t.Name)
            .FirstAsync(cancellationToken);
        var inviteLink = $"{_frontend.BaseUrl.TrimEnd('/')}/reset-password?token={inviteToken}";

        await _emailSender.SendAsync(
            request.Email,
            $"You've been added to {clinicName}",
            $"""
            <div style="font-family:Arial,sans-serif;max-width:520px;margin:auto">
              <h2 style="color:#0C2B23">Welcome to {clinicName} 👋</h2>
              <p>Hi {request.FirstName}, you've been added as
                 <strong>{string.Join(" & ", roleNames)}</strong>.</p>
              <p><a href="{inviteLink}" style="background:#00BD8F;color:#06362B;padding:12px 22px;
                 border-radius:10px;text-decoration:none;font-weight:bold">Set your password</a></p>
              <p style="color:#5B6F68;font-size:13px">
                 The link is valid for 7 days. You can also sign in with the temporary
                 password your admin gave you, and change it later.</p>
            </div>
            """,
            cancellationToken);

        return new StaffDto
        {
            Id = tenantUser.Id,
            SystemUserId = systemUser.Id,
            FullName = $"{request.FirstName} {request.LastName}",
            Email = request.Email,
            Roles = roleNames,
            IsActive = true,
            CreatedAt = tenantUser.CreatedAt
        };
    }

    public async Task<PagedResult<StaffDto>> GetAllStaffAsync(
        PageRequest page,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _currentUser.TenantId;

        // Get all staff for this clinic only
        // Using Select for DTO projection — never return entities directly
        return await _context.TenantUsers
            .AsNoTracking()
            .Where(tu => tu.TenantId == tenantId)
            .OrderBy(tu => tu.CreatedAt)   // stable order BEFORE Skip/Take
            .Select(tu => new StaffDto
            {
                Id = tu.Id,
                SystemUserId = tu.SystemUserId,
                FullName = tu.SystemUser.FirstName + " " + tu.SystemUser.LastName,
                Email = tu.SystemUser.Email,
                Roles = tu.Roles.Select(r => r.Role.Name).ToList(),
                IsActive = tu.IsActive,
                CreatedAt = tu.CreatedAt
            })
            .ToPagedResultAsync(page, cancellationToken);
    }
}