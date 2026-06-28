using Clinic.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Clinic.Infrastructure.Persistence.Configurations;

public class PatientMedicalConditionConfiguration
    : IEntityTypeConfiguration<PatientMedicalCondition>
{
    public void Configure(EntityTypeBuilder<PatientMedicalCondition> builder)
    {
        builder.ToTable("PatientMedicalConditions");
        builder.HasKey(x => new { x.PatientId, x.MedicalConditionId });

        builder.Property(x => x.Notes).HasMaxLength(500);

        builder.HasOne(x => x.Patient)
               .WithMany(p => p.MedicalConditions)
               .HasForeignKey(x => x.PatientId)
               .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.MedicalCondition)
               .WithMany()
               .HasForeignKey(x => x.MedicalConditionId)
               .OnDelete(DeleteBehavior.Restrict);
    }
}