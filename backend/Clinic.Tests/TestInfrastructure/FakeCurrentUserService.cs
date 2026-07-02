using Clinic.Application.Common.Interfaces;

namespace Clinic.Tests.TestInfrastructure;

/// <summary>
/// Test double for ICurrentUserService — lets a test act as any user of any
/// tenant by simply setting properties. The DbContext's tenant query filter
/// reads TenantId per query, so changing it mid-test switches "who is asking".
/// </summary>
public class FakeCurrentUserService : ICurrentUserService
{
    public Guid UserId { get; set; } = Guid.Empty;
    public Guid TenantId { get; set; } = Guid.Empty;
    public Guid TenantUserId { get; set; } = Guid.Empty;
    public string? Email { get; set; }
    public IEnumerable<string> Roles { get; set; } = [];
    public bool IsAuthenticated { get; set; }

    public void ActAs(Guid tenantId, Guid tenantUserId, Guid userId = default)
    {
        TenantId = tenantId;
        TenantUserId = tenantUserId;
        UserId = userId == default ? Guid.NewGuid() : userId;
        IsAuthenticated = true;
    }

    public void ActAsAnonymous()
    {
        TenantId = Guid.Empty;
        TenantUserId = Guid.Empty;
        UserId = Guid.Empty;
        IsAuthenticated = false;
    }
}
