using Clinic.Application.Common.Interfaces;
using Clinic.Domain.Common;
using Clinic.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;
using System.Reflection;

namespace Clinic.Infrastructure.Persistence;

public class ClinicDbContext : DbContext
{
    private readonly ICurrentUserService _currentUser;

    public ClinicDbContext(
        DbContextOptions<ClinicDbContext> options,
        ICurrentUserService currentUser) : base(options)
    {
        _currentUser = currentUser;
    }

    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<SystemUser> SystemUsers => Set<SystemUser>();
    public DbSet<TenantUser> TenantUsers => Set<TenantUser>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<TenantUserRole> TenantUserRoles => Set<TenantUserRole>();
    public DbSet<Patient> Patients => Set<Patient>();
    public DbSet<MedicalCondition> MedicalConditions => Set<MedicalCondition>();
    public DbSet<PatientMedicalCondition> PatientMedicalConditions => Set<PatientMedicalCondition>();
    public DbSet<Appointment> Appointments => Set<Appointment>();
    public DbSet<Consultation> Consultations => Set<Consultation>();
    public DbSet<Prescription> Prescriptions => Set<Prescription>();
    public DbSet<PrescriptionItem> PrescriptionItems => Set<PrescriptionItem>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<PasswordResetToken> PasswordResetTokens => Set<PasswordResetToken>();
    public DbSet<InventoryItem> InventoryItems => Set<InventoryItem>();
    public DbSet<PlatformPayment> PlatformPayments => Set<PlatformPayment>();
    // Evaluated per query — EF captures the context instance, so each request's
    // scoped ICurrentUserService supplies the tenant from the verified JWT.
    private Guid CurrentTenantId => _currentUser.TenantId;

    protected override void OnModelCreating(ModelBuilder builder)
    {
        builder.HasDefaultSchema("clinic");
        builder.ApplyConfigurationsFromAssembly(typeof(ClinicDbContext).Assembly);

        // Join tables don't implement ISoftDelete themselves — filter via their parents
        builder.Entity<TenantUserRole>()
            .HasQueryFilter(QueryFilters.SoftDelete,
                x => !x.Role.IsDeleted && !x.TenantUser.IsDeleted);

        builder.Entity<PatientMedicalCondition>()
            .HasQueryFilter(QueryFilters.SoftDelete,
                x => !x.MedicalCondition.IsDeleted && !x.Patient.IsDeleted);

        // PrescriptionItem has no TenantId of its own — it inherits both
        // filters through its parent prescription
        builder.Entity<PrescriptionItem>()
            .HasQueryFilter(QueryFilters.SoftDelete, x => !x.Prescription.IsDeleted);
        builder.Entity<PrescriptionItem>()
            .HasQueryFilter(QueryFilters.Tenant, x => x.Prescription.TenantId == CurrentTenantId);

        foreach (var entityType in builder.Model.GetEntityTypes())
        {
            // Auto soft delete filter for every entity implementing ISoftDelete
            if (typeof(ISoftDelete).IsAssignableFrom(entityType.ClrType))
            {
                var filter = (LambdaExpression)typeof(ClinicDbContext)
                    .GetMethod(nameof(GetSoftDeleteFilter),
                        BindingFlags.NonPublic | BindingFlags.Static)!
                    .MakeGenericMethod(entityType.ClrType)
                    .Invoke(null, null)!;

                builder.Entity(entityType.ClrType)
                    .HasQueryFilter(QueryFilters.SoftDelete, filter);
            }

            // Tenant isolation: every IMustHaveTenant entity is automatically scoped
            // to the current tenant in EVERY query. A forgotten Where() can no longer
            // leak another clinic's data.
            if (typeof(IMustHaveTenant).IsAssignableFrom(entityType.ClrType))
            {
                var filter = (LambdaExpression)typeof(ClinicDbContext)
                    .GetMethod(nameof(GetTenantFilter),
                        BindingFlags.NonPublic | BindingFlags.Instance)!
                    .MakeGenericMethod(entityType.ClrType)
                    .Invoke(this, null)!;

                builder.Entity(entityType.ClrType)
                    .HasQueryFilter(QueryFilters.Tenant, filter);
            }
        }

        base.OnModelCreating(builder);
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        EnforceTenantOnWrites();

        var now = DateTime.UtcNow;
        var userId = _currentUser.IsAuthenticated ? _currentUser.UserId : Guid.Empty;

        foreach (var entry in ChangeTracker.Entries<IAuditableEntity>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    entry.Entity.CreatedAt = now;
                    entry.Entity.CreatedBy = userId;
                    break;

                case EntityState.Modified:
                    entry.Entity.ModifiedAt = now;
                    entry.Entity.ModifiedBy = userId;
                    entry.Property(nameof(IAuditableEntity.CreatedAt)).IsModified = false;
                    entry.Property(nameof(IAuditableEntity.CreatedBy)).IsModified = false;
                    break;
            }
        }

        return await base.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// The rare legit cross-tenant writes: (1) an Admin opening a NEW clinic
    /// provisions its first TenantUser/Roles while their token is still scoped
    /// to the old clinic; (2) the PLATFORM console (email-allowlisted owner)
    /// writes billing notifications into a customer clinic. This whitelists
    /// exactly ONE tenant id, for this request only (the context is
    /// request-scoped) — writes into any other tenant stay blocked.
    /// </summary>
    private Guid? _crossTenantWriteTenantId;

    public void AllowCrossTenantWritesFor(Guid tenantId) => _crossTenantWriteTenantId = tenantId;

    /// <summary>
    /// Write-side tenant protection:
    /// - New entities without a TenantId get the current tenant stamped automatically.
    /// - Writing an entity that belongs to a DIFFERENT tenant than the caller is blocked.
    /// (Registration is the exception: it creates a brand-new tenant while unauthenticated,
    /// which is allowed because the entity carries an explicit TenantId.)
    /// </summary>
    private void EnforceTenantOnWrites()
    {
        foreach (var entry in ChangeTracker.Entries<IMustHaveTenant>())
        {
            if (entry.State is not (EntityState.Added or EntityState.Modified))
                continue;

            var tenantProperty = entry.Property(nameof(IMustHaveTenant.TenantId));
            var entityTenantId = (Guid)tenantProperty.CurrentValue!;

            if (entry.State == EntityState.Added && entityTenantId == Guid.Empty)
            {
                if (CurrentTenantId == Guid.Empty)
                    throw new InvalidOperationException(
                        $"Cannot save '{entry.Metadata.ClrType.Name}' without a TenantId " +
                        "when no tenant is resolved from the current user.");

                tenantProperty.CurrentValue = CurrentTenantId;
            }
            else if (CurrentTenantId != Guid.Empty
                     && entityTenantId != CurrentTenantId
                     && entityTenantId != _crossTenantWriteTenantId)
            {
                throw new InvalidOperationException(
                    $"Cross-tenant write blocked: '{entry.Metadata.ClrType.Name}' belongs to " +
                    "a different tenant than the current user.");
            }
        }
    }

    private static Expression<Func<T, bool>> GetSoftDeleteFilter<T>()
        where T : class, ISoftDelete => x => !x.IsDeleted;

    private Expression<Func<T, bool>> GetTenantFilter<T>()
        where T : class, IMustHaveTenant => x => x.TenantId == CurrentTenantId;
}