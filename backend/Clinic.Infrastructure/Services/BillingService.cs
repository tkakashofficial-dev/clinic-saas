using Clinic.Application.Common.Exceptions;
using Clinic.Application.Common.Interfaces;
using Clinic.Application.Features.Billing.DTOs;
using Clinic.Application.Features.Billing.Services;
using Clinic.Domain.Constants;
using Clinic.Domain.Enums;
using Clinic.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Clinic.Infrastructure.Services;

public class BillingService : IBillingService
{
    private readonly ClinicDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public BillingService(ClinicDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<BillingSummaryDto> GetSummaryAsync(CancellationToken cancellationToken = default)
    {
        var tenant = await _context.Tenants
            .AsNoTracking()
            .FirstAsync(t => t.Id == _currentUser.TenantId, cancellationToken);

        return await BuildSummaryAsync(tenant, cancellationToken);
    }

    public async Task<BillingSummaryDto> ChangePlanAsync(
        ChangePlanRequest request, CancellationToken cancellationToken = default)
    {
        // IsDefined guard: Enum.TryParse happily accepts "999" -> (PlanType)999
        if (!Enum.TryParse<PlanType>(request.Plan, true, out var newPlan)
            || !Enum.IsDefined(newPlan))
            throw new BadRequestException(
                $"Unknown plan '{request.Plan}'. Available: {string.Join(", ", Enum.GetNames<PlanType>())}.");

        // Serializable: don't let a concurrent staff-add slip past the downgrade guard
        await using var transaction = await _context.Database
            .BeginTransactionAsync(System.Data.IsolationLevel.Serializable, cancellationToken);

        var tenant = await _context.Tenants
            .FirstAsync(t => t.Id == _currentUser.TenantId, cancellationToken);

        // Downgrade guard: don't allow a plan the clinic already outgrew
        var staffCount = await CountStaffAsync(cancellationToken);
        var doctorCount = await CountDoctorsAsync(cancellationToken);
        if (staffCount > PlanLimits.MaxStaff(newPlan) || doctorCount > PlanLimits.MaxDoctors(newPlan))
            throw new PlanLimitException(
                $"Your clinic has {staffCount} staff ({doctorCount} doctors) — more than the " +
                $"{PlanLimits.DisplayName(newPlan)} plan allows. Deactivate members first or pick a larger plan.");

        // Beta: instant switch. Payment gateway (Razorpay) slots in here later.
        tenant.ChangePlan(newPlan);
        await _context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return await BuildSummaryAsync(tenant, cancellationToken);
    }

    private async Task<BillingSummaryDto> BuildSummaryAsync(
        Domain.Entities.Tenant tenant, CancellationToken cancellationToken)
    {
        // EffectivePlan handles all three states: trialing, trial-lapsed (Solo
        // floor), and chosen plan — the summary must never diverge from enforcement
        var effectivePlan = tenant.EffectivePlan;

        return new BillingSummaryDto
        {
            Plan = PlanLimits.DisplayName(effectivePlan),
            IsInTrial = tenant.IsInTrial,
            TrialExpired = tenant.TrialExpired,
            TrialEndsAt = tenant.TrialEndsAt,
            StaffCount = await CountStaffAsync(cancellationToken),
            MaxStaff = PlanLimits.MaxStaff(effectivePlan),
            DoctorCount = await CountDoctorsAsync(cancellationToken),
            MaxDoctors = PlanLimits.MaxDoctors(effectivePlan)
        };
    }

    private Task<int> CountStaffAsync(CancellationToken ct) =>
        _context.TenantUsers.CountAsync(tu => tu.IsActive, ct); // tenant filter scopes it

    private Task<int> CountDoctorsAsync(CancellationToken ct) =>
        _context.TenantUsers.CountAsync(
            tu => tu.IsActive && tu.Roles.Any(r => r.Role.Name == RoleNames.Doctor), ct);
}
