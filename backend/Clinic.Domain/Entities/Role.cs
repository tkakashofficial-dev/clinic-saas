using Clinic.Domain.Common;

namespace Clinic.Domain.Entities;

public class Role : BaseEntity, IMustHaveTenant
{
    public Guid TenantId { get; private set; }
    public string Name { get; private set; } = default!;
    public string? Description { get; private set; }

    private Role() { }

    public Role(Guid tenantId, string name, string? description = null)
    {
        TenantId = tenantId;
        Name = name ?? throw new ArgumentNullException(nameof(name));
        Description = description;
    }
}