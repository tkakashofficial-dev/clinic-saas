using Clinic.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Clinic.Infrastructure.Persistence.Configurations;

public class IntakeFormResponseConfiguration : IEntityTypeConfiguration<IntakeFormResponse>
{
    public void Configure(EntityTypeBuilder<IntakeFormResponse> builder)
    {
        builder.ToTable("IntakeFormResponses");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Template).IsRequired().HasMaxLength(10);
        builder.Property(x => x.AnswersJson).IsRequired().HasMaxLength(12000);

        builder.HasOne(x => x.Patient)
               .WithMany()
               .HasForeignKey(x => x.PatientId)
               .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.FilledBy)
               .WithMany()
               .HasForeignKey(x => x.FilledByTenantUserId)
               .OnDelete(DeleteBehavior.Restrict);

        // "Latest answers for this patient" is the hot query
        builder.HasIndex(x => new { x.TenantId, x.PatientId, x.CreatedAt });
    }
}
