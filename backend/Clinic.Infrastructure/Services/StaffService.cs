using Clinic.Application.Common.Exceptions;
using Clinic.Application.Common.Interfaces;
using Clinic.Application.Features.Staff.DTOs;
using Clinic.Application.Features.Staff.Services;
using Clinic.Domain.Constants;
using Clinic.Domain.Entities;
using Clinic.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Clinic.Infrastructure.Services;

public class StaffService : IStaffService
{
    private readonly ClinicDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public StaffService(ClinicDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<StaffDto> AddStaffAsync(
        AddStaffRequest request,
        CancellationToken cancellationToken = default)
    {
        // TenantId comes from JWT token — never from request body
        var tenantId = _currentUser.TenantId;

        // 0. Roles are a closed set — free-text would silently break
        //    [Authorize(Roles = ...)] matching (e.g. a typo like "Docter")
        if (!RoleNames.TryNormalize(request.Role, out var roleName))
            throw new BadRequestException(
                $"Invalid role '{request.Role}'. Allowed roles: {string.Join(", ", RoleNames.All)}.");

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

        // 4. Find the seeded role (create only as fallback for tenants
        //    registered before role seeding existed)
        var role = await _context.Roles
            .FirstOrDefaultAsync(r => r.TenantId == tenantId
                && r.Name == roleName, cancellationToken);

        if (role is null)
        {
            role = new Role(tenantId, roleName, RoleNames.DescriptionOf(roleName));
            _context.Roles.Add(role);
        }

        // 5. Assign role
        var tenantUserRole = new TenantUserRole(tenantUser.Id, role.Id);
        _context.TenantUserRoles.Add(tenantUserRole);

        // 6. Save everything
        await _context.SaveChangesAsync(cancellationToken);

        return new StaffDto
        {
            Id = tenantUser.Id,
            SystemUserId = systemUser.Id,
            FullName = $"{request.FirstName} {request.LastName}",
            Email = request.Email,
            Role = roleName,
            IsActive = true,
            CreatedAt = tenantUser.CreatedAt
        };
    }

    public async Task<List<StaffDto>> GetAllStaffAsync(
        CancellationToken cancellationToken = default)
    {
        var tenantId = _currentUser.TenantId;

        // Get all staff for this clinic only
        // Using Select for DTO projection — never return entities directly
        return await _context.TenantUsers
            .AsNoTracking()
            .Where(tu => tu.TenantId == tenantId)
            .Select(tu => new StaffDto
            {
                Id = tu.Id,
                SystemUserId = tu.SystemUserId,
                FullName = tu.SystemUser.FirstName + " " + tu.SystemUser.LastName,
                Email = tu.SystemUser.Email,
                Role = tu.Roles
                    .Select(r => r.Role.Name)
                    .FirstOrDefault() ?? "Unknown",
                IsActive = tu.IsActive,
                CreatedAt = tu.CreatedAt
            })
            .ToListAsync(cancellationToken);
    }
}