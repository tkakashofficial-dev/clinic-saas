using Clinic.Domain.Common;
using Clinic.Domain.Enums;

namespace Clinic.Domain.Entities;

public class Patient : BaseEntity, IMustHaveTenant
{
    public Guid TenantId { get; private set; }
    public Guid RegisteredByTenantUserId { get; private set; }

    public string FirstName { get; private set; } = default!;
    public string LastName { get; private set; } = default!;
    public string Phone { get; private set; } = default!;
    public string? Email { get; private set; }
    public string? Address { get; private set; }
    public Gender Gender { get; private set; }
    public DateOnly? DateOfBirth { get; private set; }

    public Tenant Tenant { get; private set; } = default!;
    public TenantUser RegisteredBy { get; private set; } = default!;

    private readonly List<PatientMedicalCondition> _medicalConditions = new();
    public IReadOnlyCollection<PatientMedicalCondition> MedicalConditions
        => _medicalConditions;

    private Patient() { }

    public Patient(
        Guid tenantId,
        Guid registeredByTenantUserId,
        string firstName,
        string lastName,
        string phone,
        Gender gender,
        DateOnly? dateOfBirth,
        string? email = null,
        string? address = null)
    {
        TenantId = tenantId;
        RegisteredByTenantUserId = registeredByTenantUserId;
        FirstName = firstName ?? throw new ArgumentNullException(nameof(firstName));
        LastName = lastName ?? throw new ArgumentNullException(nameof(lastName));
        Phone = phone ?? throw new ArgumentNullException(nameof(phone));
        Gender = gender;
        DateOfBirth = dateOfBirth;
        Email = email;
        Address = address;
    }
}