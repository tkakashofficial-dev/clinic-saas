using Clinic.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Clinic.Infrastructure.Persistence.Configurations;

public class MedicalConditionConfiguration : IEntityTypeConfiguration<MedicalCondition>
{
    public void Configure(EntityTypeBuilder<MedicalCondition> builder)
    {
        builder.ToTable("MedicalConditions");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Name).IsRequired().HasMaxLength(200);
        builder.Property(x => x.Code).IsRequired().HasMaxLength(50);
        builder.HasIndex(x => x.Code).IsUnique();

        // Seed data — these conditions are fixed in the system
        builder.HasData(
      new
      {
          Id = Guid.Parse("10000000-0000-0000-0000-000000000001"),
          Name = "Diabetes",
          Code = "DIABETES",
          CreatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
          IsDeleted = false,
          CreatedBy = (Guid?)null,
          ModifiedAt = (DateTime?)null,
          ModifiedBy = (Guid?)null,
          DeletedAt = (DateTime?)null,
          DeletedBy = (Guid?)null
      },
      new
      {
          Id = Guid.Parse("10000000-0000-0000-0000-000000000002"),
          Name = "Hypertension",
          Code = "HYPERTENSION",
          CreatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
          IsDeleted = false,
          CreatedBy = (Guid?)null,
          ModifiedAt = (DateTime?)null,
          ModifiedBy = (Guid?)null,
          DeletedAt = (DateTime?)null,
          DeletedBy = (Guid?)null
      },
      new
      {
          Id = Guid.Parse("10000000-0000-0000-0000-000000000003"),
          Name = "Drug Allergy",
          Code = "DRUG_ALLERGY",
          CreatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
          IsDeleted = false,
          CreatedBy = (Guid?)null,
          ModifiedAt = (DateTime?)null,
          ModifiedBy = (Guid?)null,
          DeletedAt = (DateTime?)null,
          DeletedBy = (Guid?)null
      },
      new
      {
          Id = Guid.Parse("10000000-0000-0000-0000-000000000004"),
          Name = "Latex Allergy",
          Code = "LATEX_ALLERGY",
          CreatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
          IsDeleted = false,
          CreatedBy = (Guid?)null,
          ModifiedAt = (DateTime?)null,
          ModifiedBy = (Guid?)null,
          DeletedAt = (DateTime?)null,
          DeletedBy = (Guid?)null
      },
      new
      {
          Id = Guid.Parse("10000000-0000-0000-0000-000000000005"),
          Name = "Pregnancy",
          Code = "PREGNANCY",
          CreatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
          IsDeleted = false,
          CreatedBy = (Guid?)null,
          ModifiedAt = (DateTime?)null,
          ModifiedBy = (Guid?)null,
          DeletedAt = (DateTime?)null,
          DeletedBy = (Guid?)null
      },
      new
      {
          Id = Guid.Parse("10000000-0000-0000-0000-000000000006"),
          Name = "Blood Pressure",
          Code = "BLOOD_PRESSURE",
          CreatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
          IsDeleted = false,
          CreatedBy = (Guid?)null,
          ModifiedAt = (DateTime?)null,
          ModifiedBy = (Guid?)null,
          DeletedAt = (DateTime?)null,
          DeletedBy = (Guid?)null
      },
      new
      {
          Id = Guid.Parse("10000000-0000-0000-0000-000000000007"),
          Name = "Cardiac Issues",
          Code = "CARDIAC",
          CreatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
          IsDeleted = false,
          CreatedBy = (Guid?)null,
          ModifiedAt = (DateTime?)null,
          ModifiedBy = (Guid?)null,
          DeletedAt = (DateTime?)null,
          DeletedBy = (Guid?)null
      }
  );
    }
}