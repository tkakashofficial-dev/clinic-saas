using System.Text.RegularExpressions;
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

        // UPI IDs look like handle@bank ("6238456205@ybl", "smile.dental@okaxis")
        var upiId = NullIfEmpty(request.UpiId)?.ToLowerInvariant();
        if (upiId is not null && !Regex.IsMatch(upiId, @"^[a-z0-9.\-_]{2,64}@[a-z]{2,32}$"))
            throw new BadRequestException(
                "That doesn't look like a UPI ID — expected something like clinicname@okaxis.");

        var tenant = await GetTenantAsync(cancellationToken);

        // Name/phone/address print on every prescription and intake form —
        // this page is where the clinic's letterhead is born
        tenant.Update(
            name,
            NullIfEmpty(request.Phone),
            NullIfEmpty(request.Address));
        tenant.SetDefaultIntakeTemplate(template);
        tenant.SetUpiId(upiId);

        // First time booking is switched on, mint the permanent public URL
        if (request.PublicBookingEnabled && tenant.Slug is null)
            tenant.AssignSlug(await GenerateUniqueSlugAsync(name, cancellationToken));
        tenant.SetPublicBooking(request.PublicBookingEnabled);

        await _context.SaveChangesAsync(cancellationToken);
        return MapToDto(tenant);
    }

    /// <summary>"Smile Dental — Kochi" → "smile-dental-kochi", with a numeric
    /// suffix if another clinic already claimed it.</summary>
    private async Task<string> GenerateUniqueSlugAsync(string name, CancellationToken ct)
    {
        var baseSlug = Regex.Replace(name.ToLowerInvariant(), @"[^a-z0-9]+", "-").Trim('-');
        if (baseSlug.Length > 60) baseSlug = baseSlug[..60].Trim('-');
        if (baseSlug.Length == 0) baseSlug = "clinic";

        var slug = baseSlug;
        for (var i = 2; await _context.Tenants.IgnoreQueryFilters()
                 .AnyAsync(t => t.Slug == slug, ct); i++)
            slug = $"{baseSlug}-{i}";
        return slug;
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
        DefaultIntakeTemplate = tenant.DefaultIntakeTemplate,
        UpiId = tenant.UpiId,
        Slug = tenant.Slug,
        PublicBookingEnabled = tenant.PublicBookingEnabled
    };
}
