using Clinic.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Clinic.Infrastructure.Persistence.Configurations;

public class PatientConfiguration : IEntityTypeConfiguration<Patient>
{
    public void Configure(EntityTypeBuilder<Patient> builder)
    {
        builder.ToTable("Patients");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.FirstName).IsRequired().HasMaxLength(100);
        builder.Property(x => x.LastName).IsRequired().HasMaxLength(100);
        builder.Property(x => x.Phone).IsRequired().HasMaxLength(20);
        builder.Property(x => x.Email).HasMaxLength(256);
        builder.Property(x => x.Address).HasMaxLength(500);
        builder.Property(x => x.Gender).HasConversion<string>().HasMaxLength(10);
        // DateOfBirth is optional — the entity (DateOnly?), DTO and age
        // calculation all treat it as nullable, so the column must match
        builder.Property(x => x.DateOfBirth);
        builder.Property(x => x.BloodGroup).HasMaxLength(3);   // longest is "AB+"

        builder.HasOne(x => x.Tenant)
               .WithMany()
               .HasForeignKey(x => x.TenantId)
               .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.RegisteredBy)
               .WithMany()
               .HasForeignKey(x => x.RegisteredByTenantUserId)
               .OnDelete(DeleteBehavior.Restrict);

        // Phone unique per tenant — same phone cannot register twice in same clinic
        builder.HasIndex(x => new { x.TenantId, x.Phone }).IsUnique();

        // Patient numbers are per-clinic sequences — DB enforces no duplicates
        builder.HasIndex(x => new { x.TenantId, x.PatientNumber }).IsUnique();
    }
}