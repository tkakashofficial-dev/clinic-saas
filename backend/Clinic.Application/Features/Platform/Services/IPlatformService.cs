using Clinic.Application.Features.Platform.DTOs;

namespace Clinic.Application.Features.Platform.Services;

/// <summary>
/// The SaaS owner's back-office: every clinic on the platform. Access is
/// restricted to configured platform-admin emails — this is the ONLY place
/// that intentionally crosses tenant boundaries (metadata only, never
/// patient data).
/// </summary>
public interface IPlatformService
{
    Task<List<PlatformTenantDto>> GetTenantsAsync(CancellationToken cancellationToken = default);
    Task<PlatformTenantDto> ChangePlanAsync(Guid tenantId, PlatformChangePlanRequest request, CancellationToken cancellationToken = default);
    Task<PlatformTenantDto> SetActiveAsync(Guid tenantId, PlatformSetActiveRequest request, CancellationToken cancellationToken = default);

    /// <summary>Sends a real email to the platform admin — proves production
    /// SMTP works end-to-end (or reports that it doesn't, with a reason).</summary>
    Task<PlatformEmailTestResult> SendTestEmailAsync(CancellationToken cancellationToken = default);
}
