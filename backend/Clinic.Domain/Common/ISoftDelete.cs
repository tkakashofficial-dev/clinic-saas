namespace Clinic.Domain.Common;

public interface ISoftDelete
{
    bool IsDeleted { get; }
    DateTime? DeletedAt { get; }
    Guid? DeletedBy { get; }
    void Delete(Guid deletedBy);
}