using Clinic.Domain.Common;

namespace Clinic.Domain.Entities;

public class TenantUser : BaseEntity, IMustHaveTenant
{
    public Guid TenantId { get; private set; }
    public Guid SystemUserId { get; private set; }
    public bool IsActive { get; private set; } = true;

    public Tenant Tenant { get; private set; } = default!;
    public SystemUser SystemUser { get; private set; } = default!;

    private readonly List<TenantUserRole> _roles = new();
    public IReadOnlyCollection<TenantUserRole> Roles => _roles;

    private TenantUser() { }

    public TenantUser(Guid tenantId, Guid systemUserId)
    {
        TenantId = tenantId;
        SystemUserId = systemUserId;
    }

    public void AssignRole(TenantUserRole role) => _roles.Add(role);
    public void Deactivate() => IsActive = false;
    public void Activate() => IsActive = true;
}