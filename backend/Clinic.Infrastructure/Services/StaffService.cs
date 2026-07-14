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

        // 1. Does this person already have a Klivia account (e.g. a visiting
        //    doctor who works at another clinic)? Then we ATTACH a membership —
        //    we never create a duplicate account and, critically, we never
        //    touch their password: this clinic's admin must have no power over
        //    an account that belongs to another clinic's staff member.
        var existingUser = await _context.SystemUsers
            .FirstOrDefaultAsync(u => u.Email == request.Email, cancellationToken);

        var isExistingAccount = existingUser is not null;
        var inviteOnly = string.IsNullOrWhiteSpace(request.Password);

        SystemUser systemUser;
        if (existingUser is not null)
        {
            if (!existingUser.IsActive)
                throw new ConflictException("This account has been deactivated.");

            var alreadyMember = await _context.TenantUsers
                .IgnoreQueryFilters([QueryFilters.Tenant])
                .AnyAsync(tu => tu.SystemUserId == existingUser.Id && tu.TenantId == tenantId,
                    cancellationToken);
            if (alreadyMember)
                throw new ConflictException("This person is already a member of this clinic.");

            systemUser = existingUser; // password and profile stay untouched
        }
        else
        {
            // 2. New person: create the account. Invite-only (no password
            //    given): hash an unguessable random secret — the account is
            //    unusable until they set their own password via the email link.
            var passwordHash = BCrypt.Net.BCrypt.HashPassword(
                inviteOnly ? SecurityTokens.CreateUrlSafe() : request.Password!);
            systemUser = new SystemUser(
                request.Email,
                passwordHash,
                request.FirstName,
                request.LastName);

            _context.SystemUsers.Add(systemUser);
        }

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

        // 5. Set-password link — ONLY for brand-new accounts. Existing users
        //    keep their own password; this admin gets no way to change it.
        string? inviteToken = null;
        if (!isExistingAccount)
        {
            inviteToken = SecurityTokens.CreateUrlSafe();
            _context.PasswordResetTokens.Add(new PasswordResetToken(
                systemUser.Id,
                SecurityTokens.Sha256Hex(inviteToken),
                DateTime.UtcNow.AddDays(7)));
        }

        // 6. Save everything and release the serializable transaction
        await _context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        // 7. Email is intentionally fire-and-forget so staff creation does
        //    not feel slow when mail delivery is delayed. Existing accounts
        //    get "this clinic was added — use the switcher"; new accounts get
        //    the set-your-password invite.
        var clinicName = await _context.Tenants
            .Where(t => t.Id == tenantId)
            .Select(t => t.Name)
            .FirstAsync(cancellationToken);
        var rolesLabel = string.Join(" & ", roleNames);
        var baseUrl = _frontend.BaseUrl.TrimEnd('/');

        var emailBody = isExistingAccount
            ? EmailTemplates.AddedToClinic(
                systemUser.FirstName, clinicName, rolesLabel, $"{baseUrl}/login")
            : EmailTemplates.StaffInvite(
                request.FirstName, clinicName, rolesLabel,
                $"{baseUrl}/accept-invite?token={inviteToken}",
                hasTempPassword: !inviteOnly);

        _ = _emailSender.SendAsync(
            request.Email,
            $"You've been added to {clinicName}",
            emailBody,
            CancellationToken.None);

        return new StaffDto
        {
            Id = tenantUser.Id,
            SystemUserId = systemUser.Id,
            // For existing accounts, keep THEIR real name (admin's typed name is ignored)
            FullName = $"{systemUser.FirstName} {systemUser.LastName}",
            Email = request.Email,
            Roles = roleNames,
            IsActive = true,
            ExistingAccount = isExistingAccount,
            CreatedAt = tenantUser.CreatedAt
        };
    }

    public async Task ResendInviteAsync(
        Guid tenantUserId,
        CancellationToken cancellationToken = default)
    {
        // Tenant filter scopes this: admins can only re-invite their own staff
        var tenantUser = await _context.TenantUsers
            .Include(tu => tu.SystemUser)
            .Include(tu => tu.Roles).ThenInclude(r => r.Role)
            .FirstOrDefaultAsync(tu => tu.Id == tenantUserId, cancellationToken)
            ?? throw new NotFoundException("Staff member not found.");

        if (!tenantUser.IsActive || !tenantUser.SystemUser.IsActive)
            throw new ConflictException("This staff member is deactivated.");

        var inviteToken = SecurityTokens.CreateUrlSafe();
        _context.PasswordResetTokens.Add(new PasswordResetToken(
            tenantUser.SystemUserId,
            SecurityTokens.Sha256Hex(inviteToken),
            DateTime.UtcNow.AddDays(7)));
        await _context.SaveChangesAsync(cancellationToken);

        var clinicName = await _context.Tenants
            .Where(t => t.Id == _currentUser.TenantId)
            .Select(t => t.Name)
            .FirstAsync(cancellationToken);

        _ = _emailSender.SendAsync(
            tenantUser.SystemUser.Email,
            $"Your invite to {clinicName}",
            EmailTemplates.StaffInvite(
                tenantUser.SystemUser.FirstName,
                clinicName,
                string.Join(" & ", tenantUser.Roles.Select(r => r.Role.Name)),
                $"{_frontend.BaseUrl.TrimEnd('/')}/accept-invite?token={inviteToken}",
                hasTempPassword: false),
            CancellationToken.None);
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