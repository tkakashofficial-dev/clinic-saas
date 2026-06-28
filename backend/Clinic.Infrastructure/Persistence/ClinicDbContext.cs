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
    protected override void OnModelCreating(ModelBuilder builder)
    {
        builder.HasDefaultSchema("clinic");
        builder.ApplyConfigurationsFromAssembly(typeof(ClinicDbContext).Assembly);

        // Suppress soft delete filter warnings for join tables
        builder.Entity<TenantUserRole>()
            .HasQueryFilter(x => !x.Role.IsDeleted && !x.TenantUser.IsDeleted);

        builder.Entity<PatientMedicalCondition>()
            .HasQueryFilter(x => !x.MedicalCondition.IsDeleted && !x.Patient.IsDeleted);

        // Auto soft delete filter for every entity implementing ISoftDelete
        foreach (var entityType in builder.Model.GetEntityTypes())
        {
            if (typeof(ISoftDelete).IsAssignableFrom(entityType.ClrType))
            {
                var method = typeof(ClinicDbContext)
                    .GetMethod(nameof(GetSoftDeleteFilter),
                        BindingFlags.NonPublic | BindingFlags.Static)!
                    .MakeGenericMethod(entityType.ClrType);

                var filter = method.Invoke(null, null);
                builder.Entity(entityType.ClrType)
                    .HasQueryFilter((LambdaExpression)filter!);
            }
        }

        base.OnModelCreating(builder);
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
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

    private static Expression<Func<T, bool>> GetSoftDeleteFilter<T>()
        where T : class, ISoftDelete => x => !x.IsDeleted;
}