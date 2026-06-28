namespace Clinic.Domain.Common;

public interface IMustHaveTenant
{
    Guid TenantId { get; }
}