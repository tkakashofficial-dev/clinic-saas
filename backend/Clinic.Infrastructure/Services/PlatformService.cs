using Clinic.Application.Common.Exceptions;
using Clinic.Application.Common.Interfaces;
using Clinic.Application.Features.Platform.DTOs;
using Clinic.Application.Features.Platform.Services;
using Clinic.Domain.Constants;
using Clinic.Domain.Entities;
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
    private readonly IEmailSender _emailSender;
    private readonly PlatformSettings _settings;

    public PlatformService(
        ClinicDbContext context,
        ICurrentUserService currentUser,
        IEmailSender emailSender,
        IOptions<PlatformSettings> settings)
    {
        _context = context;
        _currentUser = currentUser;
        _emailSender = emailSender;
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

        // Subscription coverage: latest payment + how far the clinic is paid up
        var payments = (await _context.PlatformPayments
            .AsNoTracking()
            .GroupBy(p => p.TenantId)
            .Select(g => new
            {
                TenantId = g.Key,
                PaidUntil = g.Max(p => p.PaidUntil),
                Last = g.OrderByDescending(p => p.PaidAt)
                        .Select(p => new { p.PaidAt, p.AmountRupees })
                        .First()
            })
            .ToListAsync(cancellationToken))
            .ToDictionary(x => x.TenantId);

        var now = DateTime.UtcNow;
        return tenants.Select(t =>
        {
            var pay = payments.GetValueOrDefault(t.Id);
            var isInTrial = t.TrialEndsAt != null && t.TrialEndsAt > now;
            return new PlatformTenantDto
            {
                TenantId = t.Id,
                Name = t.Name,
                Plan = t.Plan.ToString(),
                IsInTrial = isInTrial,
                TrialEndsAt = t.TrialEndsAt,
                IsActive = t.IsActive,
                StaffCount = staffCounts.GetValueOrDefault(t.Id),
                PatientCount = patientCounts.GetValueOrDefault(t.Id),
                OwnerName = owners.TryGetValue(t.Id, out var owner) ? owner.OwnerName : null,
                OwnerEmail = owners.TryGetValue(t.Id, out var contact) ? contact.Email : null,
                ClinicPhone = t.Phone,
                ClinicAddress = t.Address,
                PaidUntil = pay?.PaidUntil,
                LastPaymentAt = pay?.Last.PaidAt,
                LastPaymentAmount = pay?.Last.AmountRupees,
                // Overdue = coverage lapsed after they had paid before.
                // (Trial clinics aren't "overdue" — they haven't bought yet.)
                PaymentOverdue = !isInTrial && pay != null && pay.PaidUntil < now,
                CreatedAt = t.CreatedAt
            };
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
        var changed = tenant.Plan != plan;
        tenant.ChangePlan(plan);

        if (changed)
        {
            // The clinic should FEEL the upgrade — congratulate them in-app
            await NotifyTenantAdminsAsync(tenantId,
                $"You're now on the {plan} plan 🎉",
                $"Congratulations! {tenant.Name} has been moved to the {plan} plan. " +
                "All its features are unlocked — enjoy the extra room.",
                cancellationToken);
        }

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

        var wasActive = tenant.IsActive;
        if (request.IsActive) tenant.Activate();
        else tenant.Deactivate();   // non-payment etc. — logins stop working

        if (request.IsActive && !wasActive)
        {
            // Re-activation is a happy moment — greet them when they're back in
            await NotifyTenantAdminsAsync(tenantId,
                "Welcome back! Your clinic is active again ✅",
                $"{tenant.Name} has been re-activated — your whole team can sign in again. " +
                "Thank you for staying with Klivia.",
                cancellationToken);
        }

        await _context.SaveChangesAsync(cancellationToken);
        return await GetOneAsync(tenantId, cancellationToken);
    }

    public async Task<PlatformTenantDto> RecordPaymentAsync(
        Guid tenantId, RecordPaymentRequest request,
        CancellationToken cancellationToken = default)
    {
        EnsurePlatformAdmin();

        if (request.AmountRupees <= 0)
            throw new BadRequestException("Amount must be greater than zero.");
        if (request.PeriodMonths is < 1 or > 24)
            throw new BadRequestException("Period must be between 1 and 24 months.");
        if (!PaymentMethods.All.Contains(request.Method))
            throw new BadRequestException(
                $"Unknown payment method. Available: {string.Join(", ", PaymentMethods.All)}.");

        var tenant = await _context.Tenants
            .FirstOrDefaultAsync(t => t.Id == tenantId, cancellationToken)
            ?? throw new NotFoundException("Clinic not found.");

        // When did the money actually arrive? Recording can lag the payment.
        var now = DateTime.UtcNow;
        var paidAt = request.PaidAt ?? now;
        if (paidAt > now.AddDays(1))
            throw new BadRequestException("Payment date cannot be in the future.");
        if (paidAt < now.AddYears(-1))
            throw new BadRequestException("Payment date looks too old — check the date.");

        // Coverage EXTENDS: paying while 2 weeks remain doesn't lose those weeks
        var currentPaidUntil = await _context.PlatformPayments
            .Where(p => p.TenantId == tenantId)
            .MaxAsync(p => (DateTime?)p.PaidUntil, cancellationToken);
        var baseDate = currentPaidUntil > paidAt ? currentPaidUntil.Value : paidAt;
        var paidUntil = baseDate.AddMonths(request.PeriodMonths);

        _context.PlatformPayments.Add(new PlatformPayment(
            tenantId, request.AmountRupees, request.Method,
            request.PeriodMonths, paidAt, paidUntil, _currentUser.Email!, request.Note));

        // Payment usually comes with a plan decision — one step, not two
        if (!string.IsNullOrWhiteSpace(request.PlanToApply))
        {
            if (!Enum.TryParse<PlanType>(request.PlanToApply, true, out var plan) || !Enum.IsDefined(plan))
                throw new BadRequestException(
                    $"Unknown plan '{request.PlanToApply}'. Available: {string.Join(", ", Enum.GetNames<PlanType>())}.");
            tenant.ChangePlan(plan);
        }

        // Thank the clinic where they'll see it — every Admin's bell
        await NotifyTenantAdminsAsync(tenantId,
            "Payment received — thank you! 🎉",
            $"We've received ₹{request.AmountRupees:N0} for {tenant.Name}. Your " +
            $"{tenant.Plan} plan is active until {paidUntil:d MMM yyyy}.",
            cancellationToken);

        await _context.SaveChangesAsync(cancellationToken);

        // A receipt in their inbox builds trust (fire-and-forget, never blocks)
        var ownerEmail = await _context.TenantUsers
            .IgnoreQueryFilters([QueryFilters.Tenant])
            .Where(tu => tu.TenantId == tenantId)
            .OrderBy(tu => tu.CreatedAt)
            .Select(tu => tu.SystemUser.Email)
            .FirstOrDefaultAsync(cancellationToken);
        if (ownerEmail is not null)
        {
            _ = _emailSender.SendAsync(ownerEmail,
                $"Payment received — {tenant.Name} is active until {paidUntil:d MMM yyyy}",
                EmailTemplates.Branded(
                    "Payment received — thank you! 🎉",
                    $"<p>We've received <strong>₹{request.AmountRupees:N0}</strong> for " +
                    $"<strong>{EmailTemplates.Safe(tenant.Name)}</strong>.</p>" +
                    $"<p>Your <strong>{tenant.Plan}</strong> plan is active until " +
                    $"<strong>{paidUntil:d MMM yyyy}</strong>. Thank you for running your " +
                    "clinic with Klivia!</p>"),
                CancellationToken.None);
        }

        return await GetOneAsync(tenantId, cancellationToken);
    }

    public async Task<List<PlatformPaymentDto>> GetPaymentsAsync(
        Guid tenantId, CancellationToken cancellationToken = default)
    {
        EnsurePlatformAdmin();

        return await _context.PlatformPayments
            .AsNoTracking()
            .Where(p => p.TenantId == tenantId)
            .OrderByDescending(p => p.PaidAt)
            .Select(p => new PlatformPaymentDto
            {
                PaidAt = p.PaidAt,
                AmountRupees = p.AmountRupees,
                Method = p.Method,
                PeriodMonths = p.PeriodMonths,
                PaidUntil = p.PaidUntil,
                Note = p.Note,
                RecordedByEmail = p.RecordedByEmail
            })
            .ToListAsync(cancellationToken);
    }

    /// <summary>Drops an in-app notification on every ADMIN of the clinic —
    /// the platform's voice inside the tenant (payments, plan changes).</summary>
    private async Task NotifyTenantAdminsAsync(
        Guid tenantId, string title, string message, CancellationToken ct)
    {
        var adminTenantUserIds = await _context.TenantUsers
            .IgnoreQueryFilters([QueryFilters.Tenant])
            .Where(tu => tu.TenantId == tenantId && tu.IsActive
                && tu.Roles.Any(r => r.Role.Name == RoleNames.Admin))
            .Select(tu => tu.Id)
            .ToListAsync(ct);

        // The platform admin's token is scoped to THEIR clinic — whitelist
        // this one tenant or the cross-tenant write guard rejects the insert
        _context.AllowCrossTenantWritesFor(tenantId);

        foreach (var recipientId in adminTenantUserIds)
            _context.Notifications.Add(new Notification(
                tenantId, recipientId, NotificationTypes.Billing, title, message));
    }

    public async Task<PlatformEmailTestResult> SendTestEmailAsync(
        CancellationToken cancellationToken = default)
    {
        EnsurePlatformAdmin();
        var to = _currentUser.Email!;

        var sent = await _emailSender.SendAsync(
            to,
            "Klivia — production email test ✅",
            EmailTemplates.Branded(
                "Production email works",
                $"<p>This test was sent from the live server at " +
                $"{DateTime.UtcNow:dd MMM yyyy HH:mm} UTC.</p>" +
                "<p>Welcome mails, staff invites and password resets are using " +
                "this same pipeline.</p>"),
            cancellationToken);

        return new PlatformEmailTestResult
        {
            Sent = sent,
            To = to,
            Detail = sent
                ? $"Handed to the mail server — check {to} (and its spam folder)."
                : "Not sent: SMTP is unconfigured or the server rejected it — " +
                  "check Email__User / Email__Password env vars and the server logs."
        };
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
