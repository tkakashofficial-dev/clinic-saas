using Clinic.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Clinic.Infrastructure.Persistence.Configurations;

public class PrescriptionConfiguration : IEntityTypeConfiguration<Prescription>
{
    public void Configure(EntityTypeBuilder<Prescription> builder)
    {
        builder.ToTable("Prescriptions");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Notes).HasMaxLength(1000);

        // One prescription per consultation
        builder.HasOne(x => x.Consultation)
               .WithOne(c => c.Prescription)
               .HasForeignKey<Prescription>(x => x.ConsultationId)
               .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.ConsultationId).IsUnique();
        builder.HasIndex(x => x.TenantId);

        builder.Metadata
               .FindNavigation(nameof(Prescription.Items))!
               .SetPropertyAccessMode(PropertyAccessMode.Field);
    }
}
