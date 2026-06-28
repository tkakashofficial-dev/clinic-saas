using Clinic.Domain.Common;

namespace Clinic.Domain.Entities;

public class Tenant : BaseEntity
{
    public string Name { get; private set; } = default!;
    public string? Phone { get; private set; }
    public string? Address { get; private set; }
    public bool IsActive { get; private set; } = true;

    private Tenant() { }

    public Tenant(string name, string? phone = null, string? address = null)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        Phone = phone;
        Address = address;
    }

    public void Update(string name, string? phone, string? address)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        Phone = phone;
        Address = address;
    }

    public void Deactivate() => IsActive = false;
    public void Activate() => IsActive = true;
}