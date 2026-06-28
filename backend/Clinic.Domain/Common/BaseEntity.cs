namespace Clinic.Domain.Common;

public abstract class BaseEntity : IAuditableEntity, ISoftDelete
{
    public Guid Id { get; set; } = Guid.NewGuid();

    // IAuditableEntity — DbContext fills these automatically
    public DateTime CreatedAt { get; set; }
    public Guid? CreatedBy { get; set; }
    public DateTime? ModifiedAt { get; set; }
    public Guid? ModifiedBy { get; set; }

    // ISoftDelete — DbContext fills these automatically
    public bool IsDeleted { get; private set; }
    public DateTime? DeletedAt { get; private set; }
    public Guid? DeletedBy { get; private set; }

    public void Delete(Guid deletedBy)
    {
        IsDeleted = true;
        DeletedAt = DateTime.UtcNow;
        DeletedBy = deletedBy;
    }
}