using Clinic.Application.Common.Exceptions;
using Clinic.Application.Common.Interfaces;
using Clinic.Application.Features.Settings.DTOs;
using Clinic.Application.Features.Settings.Services;
using Clinic.Domain.Entities;
using Clinic.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Clinic.Infrastructure.Services;

public class ClinicSettingsService : IClinicSettingsService
{
    /// <summary>The seeded intake-form designs — v3's form builder makes this dynamic.</summary>
    private static readonly string[] KnownTemplates = ["dental", "general"];

    private readonly ClinicDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public ClinicSettingsService(ClinicDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<ClinicSettingsDto> GetAsync(CancellationToken cancellationToken = default)
    {
        var tenant = await GetTenantAsync(cancellationToken);
        return MapToDto(tenant);
    }

    public async Task<ClinicSettingsDto> UpdateAsync(
        UpdateClinicSettingsRequest request, CancellationToken cancellationToken = default)
    {
        var name = request.Name?.Trim();
        if (string.IsNullOrWhiteSpace(name))
            throw new BadRequestException("Clinic name is required.");

        var template = request.DefaultIntakeTemplate?.Trim().ToLowerInvariant() ?? "";
        if (!KnownTemplates.Contains(template))
            throw new BadRequestException(
                $"Unknown intake template. Available: {string.Join(", ", KnownTemplates)}.");

        var tenant = await GetTenantAsync(cancellationToken);

        // Name/phone/address print on every prescription and intake form —
        // this page is where the clinic's letterhead is born
        tenant.Update(
            name,
            NullIfEmpty(request.Phone),
            NullIfEmpty(request.Address));
        tenant.SetDefaultIntakeTemplate(template);

        await _context.SaveChangesAsync(cancellationToken);
        return MapToDto(tenant);
    }

    private async Task<Tenant> GetTenantAsync(CancellationToken ct)
        => await _context.Tenants
               .FirstOrDefaultAsync(t => t.Id == _currentUser.TenantId, ct)
           ?? throw new NotFoundException("Clinic not found.");

    private static string? NullIfEmpty(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static ClinicSettingsDto MapToDto(Tenant tenant) => new()
    {
        Name = tenant.Name,
        Phone = tenant.Phone,
        Address = tenant.Address,
        DefaultIntakeTemplate = tenant.DefaultIntakeTemplate
    };
}
