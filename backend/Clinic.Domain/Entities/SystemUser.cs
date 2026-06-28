using Clinic.Domain.Common;

namespace Clinic.Domain.Entities;

public class SystemUser : BaseEntity
{
    public string Email { get; private set; } = default!;
    public string PasswordHash { get; private set; } = default!;
    public string FirstName { get; private set; } = default!;
    public string LastName { get; private set; } = default!;
    public bool IsActive { get; private set; } = true;

    private readonly List<TenantUser> _tenantUsers = new();
    public IReadOnlyCollection<TenantUser> TenantUsers => _tenantUsers;

    private SystemUser() { }

    public SystemUser(string email, string passwordHash, string firstName, string lastName)
    {
        Email = email ?? throw new ArgumentNullException(nameof(email));
        PasswordHash = passwordHash ?? throw new ArgumentNullException(nameof(passwordHash));
        FirstName = firstName ?? throw new ArgumentNullException(nameof(firstName));
        LastName = lastName ?? throw new ArgumentNullException(nameof(lastName));
    }

    public void UpdatePassword(string newPasswordHash)
        => PasswordHash = newPasswordHash ?? throw new ArgumentNullException(nameof(newPasswordHash));

    public void Deactivate() => IsActive = false;
}