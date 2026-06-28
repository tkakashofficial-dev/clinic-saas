namespace Clinic.Domain.Entities;

// Pure join table — no audit needed here
public class TenantUserRole
{
    public Guid TenantUserId { get; private set; }
    public Guid RoleId { get; private set; }

    public TenantUser TenantUser { get; private set; } = default!;
    public Role Role { get; private set; } = default!;

    private TenantUserRole() { }

    public TenantUserRole(Guid tenantUserId, Guid roleId)
    {
        TenantUserId = tenantUserId;
        RoleId = roleId;
    }
}