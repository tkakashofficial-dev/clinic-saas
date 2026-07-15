using Clinic.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Clinic.Infrastructure.Persistence.Configurations;

public class IntakeFormSectionConfiguration : IEntityTypeConfiguration<IntakeFormSection>
{
    public void Configure(EntityTypeBuilder<IntakeFormSection> builder)
    {
        builder.ToTable("IntakeFormSections");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Template).IsRequired().HasMaxLength(10);
        builder.Property(x => x.Kind).IsRequired().HasMaxLength(12);
        builder.Property(x => x.Title).IsRequired().HasMaxLength(120);
        builder.Property(x => x.ItemsJson).IsRequired().HasMaxLength(4000);

        builder.HasOne(x => x.Tenant)
               .WithMany()
               .HasForeignKey(x => x.TenantId)
               .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => new { x.TenantId, x.SortOrder });
    }
}
