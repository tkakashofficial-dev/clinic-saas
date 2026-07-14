using Clinic.Application.Common.Exceptions;
using Clinic.Application.Common.Interfaces;
using Clinic.Application.Features.Platform.DTOs;
using Clinic.Application.Features.Platform.Services;
using Clinic.Domain.Constants;
using Clinic.Domain.Enums;
using Clinic.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Clinic.Infrastructure.Services;

/// <summary>Who owns the platform (payment collection, plan control, suspension).</summary>
public class PlatformSettings
{
    public List<string> AdminEmails { get; set; } = new();
}

public class PlatformService : IPlatformService
{
    private readonly ClinicDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly PlatformSettings _settings;

    public PlatformService(
        ClinicDbContext context,
        ICurrentUserService currentUser,
        IOptions<PlatformSettings> settings)
    {
        _context = context;
        _currentUser = currentUser;
        _settings = settings.Value;
    }

    public async Task<List<PlatformTenantDto>> GetTenantsAsync(
        CancellationToken cancellationToken = default)
    {
        EnsurePlatformAdmin();

        // The one intentional cross-tenant read: clinic METADATA only,
        // never patient records
        var tenants = await _context.Tenants
            .AsNoTracking()
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync(cancellationToken);

        var staffCounts = await _context.TenantUsers
            .IgnoreQueryFilters([QueryFilters.Tenant])
            .Where(tu => tu.IsActive)
            .GroupBy(tu => tu.TenantId)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(g => g.Key, g => g.Count, cancellationToken);

        var patientCounts = await _context.Patients
            .IgnoreQueryFilters([QueryFilters.Tenant])
            .GroupBy(p => p.TenantId)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(g => g.Key, g => g.Count, cancellationToken);

        // Payment collection is a phone call today — surface WHO to call:
        // the clinic's first (oldest) member is its founding Admin
        var owners = (await _context.TenantUsers
            .IgnoreQueryFilters([QueryFilters.Tenant])
            .AsNoTracking()
            .GroupBy(tu => tu.TenantId)
            .Select(g => g
                .OrderBy(tu => tu.CreatedAt)   // oldest member = founding Admin
                .Select(tu => new
                {
                    tu.TenantId,
                    OwnerName = tu.SystemUser.FirstName + " " + tu.SystemUser.LastName,
                    tu.SystemUser.Email
                })
                .First())
            .ToListAsync(cancellationToken))
            .ToDictionary(x => x.TenantId);

        return tenants.Select(t => new PlatformTenantDto
        {
            TenantId = t.Id,
            Name = t.Name,
            Plan = t.Plan.ToString(),
            IsInTrial = t.TrialEndsAt != null && t.TrialEndsAt > DateTime.UtcNow,
            TrialEndsAt = t.TrialEndsAt,
            IsActive = t.IsActive,
            StaffCount = staffCounts.GetValueOrDefault(t.Id),
            PatientCount = patientCounts.GetValueOrDefault(t.Id),
            OwnerName = owners.TryGetValue(t.Id, out var owner) ? owner.OwnerName : null,
            OwnerEmail = owners.TryGetValue(t.Id, out var contact) ? contact.Email : null,
            ClinicPhone = t.Phone,
            CreatedAt = t.CreatedAt
        }).ToList();
    }

    public async Task<PlatformTenantDto> ChangePlanAsync(
        Guid tenantId, PlatformChangePlanRequest request,
        CancellationToken cancellationToken = default)
    {
        EnsurePlatformAdmin();

        if (!Enum.TryParse<PlanType>(request.Plan, true, out var plan) || !Enum.IsDefined(plan))
            throw new BadRequestException(
                $"Unknown plan '{request.Plan}'. Available: {string.Join(", ", Enum.GetNames<PlanType>())}.");

        var tenant = await _context.Tenants
            .FirstOrDefaultAsync(t => t.Id == tenantId, cancellationToken)
            ?? throw new NotFoundException("Clinic not found.");

        // Platform override: used after manual (UPI/WhatsApp) payment until
        // the payment gateway automates this
        tenant.ChangePlan(plan);
        await _context.SaveChangesAsync(cancellationToken);

        return await GetOneAsync(tenantId, cancellationToken);
    }

    public async Task<PlatformTenantDto> SetActiveAsync(
        Guid tenantId, PlatformSetActiveRequest request,
        CancellationToken cancellationToken = default)
    {
        EnsurePlatformAdmin();

        var tenant = await _context.Tenants
            .FirstOrDefaultAsync(t => t.Id == tenantId, cancellationToken)
            ?? throw new NotFoundException("Clinic not found.");

        if (request.IsActive) tenant.Activate();
        else tenant.Deactivate();   // non-payment etc. — logins stop working

        await _context.SaveChangesAsync(cancellationToken);
        return await GetOneAsync(tenantId, cancellationToken);
    }

    /// <summary>Platform access = configured emails only. Roles don't apply here:
    /// a clinic Admin is NOT a platform admin.</summary>
    private void EnsurePlatformAdmin()
    {
        var email = _currentUser.Email;
        var isAdmin = email is not null && _settings.AdminEmails
            .Any(admin => string.Equals(admin, email, StringComparison.OrdinalIgnoreCase));
        if (!isAdmin)
            throw new UnauthorizedAccessException("Platform access is restricted.");
    }

    public static bool IsPlatformAdmin(PlatformSettings settings, string email) =>
        settings.AdminEmails.Any(a => string.Equals(a, email, StringComparison.OrdinalIgnoreCase));

    private async Task<PlatformTenantDto> GetOneAsync(Guid tenantId, CancellationToken ct)
    {
        var list = await GetTenantsAsync(ct);
        return list.First(t => t.TenantId == tenantId);
    }
}
