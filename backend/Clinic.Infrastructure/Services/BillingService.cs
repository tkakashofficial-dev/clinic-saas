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

        return await BuildSummaryAsync(tenant.Plan, tenant.IsInTrial, tenant.TrialEndsAt, cancellationToken);
    }

    public async Task<BillingSummaryDto> ChangePlanAsync(
        ChangePlanRequest request, CancellationToken cancellationToken = default)
    {
        if (!Enum.TryParse<PlanType>(request.Plan, true, out var newPlan))
            throw new BadRequestException(
                $"Unknown plan '{request.Plan}'. Available: {string.Join(", ", Enum.GetNames<PlanType>())}.");

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

        return await BuildSummaryAsync(tenant.Plan, tenant.IsInTrial, tenant.TrialEndsAt, cancellationToken);
    }

    private async Task<BillingSummaryDto> BuildSummaryAsync(
        PlanType plan, bool isInTrial, DateTime? trialEndsAt, CancellationToken cancellationToken)
    {
        var effectivePlan = isInTrial ? PlanType.Clinic : plan;

        return new BillingSummaryDto
        {
            Plan = PlanLimits.DisplayName(plan),
            IsInTrial = isInTrial,
            TrialEndsAt = trialEndsAt,
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
