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

        // 0b. Plan entitlements — limits live in PlanLimits, enforcement here.
        //     Serializable transaction closes the check-then-insert race
        //     (two concurrent adds both reading "1 staff" and both passing).
        await using var transaction = await _context.Database
            .BeginTransactionAsync(System.Data.IsolationLevel.Serializable, cancellationToken);

        var tenant = await _context.Tenants
            .AsNoTracking()
            .FirstAsync(t => t.Id == tenantId, cancellationToken);
        var effectivePlan = tenant.EffectivePlan;

        var currentStaff = await _context.TenantUsers
            .CountAsync(tu => tu.TenantId == tenantId && tu.IsActive, cancellationToken);
        if (currentStaff + 1 > PlanLimits.MaxStaff(effectivePlan))
            throw new PlanLimitException(
                $"The {PlanLimits.DisplayName(effectivePlan)} plan allows up to " +
                $"{PlanLimits.MaxStaff(effectivePlan)} staff members. Upgrade your plan to add more.");

        if (roleNames.Contains(RoleNames.Doctor))
        {
            var currentDoctors = await _context.TenantUsers
                .CountAsync(tu => tu.TenantId == tenantId && tu.IsActive
                    && tu.Roles.Any(r => r.Role.Name == RoleNames.Doctor), cancellationToken);
            if (currentDoctors + 1 > PlanLimits.MaxDoctors(effectivePlan))
                throw new PlanLimitException(
                    $"The {PlanLimits.DisplayName(effectivePlan)} plan allows up to " +
                    $"{PlanLimits.MaxDoctors(effectivePlan)} doctors. Upgrade your plan to add more.");
        }

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

        // 6. Save everything and release the serializable transaction
        await _context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        // 7. Invite email is intentionally fire-and-forget so staff creation
        //    does not feel slow when mail delivery is delayed.
        var clinicName = await _context.Tenants
            .Where(t => t.Id == tenantId)
            .Select(t => t.Name)
            .FirstAsync(cancellationToken);
        var inviteLink = $"{_frontend.BaseUrl.TrimEnd('/')}/reset-password?token={inviteToken}";

        _ = _emailSender.SendAsync(
            request.Email,
            $"You've been added to {clinicName}",
            EmailTemplates.StaffInvite(
                request.FirstName,
                clinicName,
                string.Join(" & ", roleNames),
                inviteLink),
            CancellationToken.None);

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