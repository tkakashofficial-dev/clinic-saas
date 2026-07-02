using Clinic.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Clinic.Infrastructure.Persistence.Configurations;

public class PrescriptionItemConfiguration : IEntityTypeConfiguration<PrescriptionItem>
{
    public void Configure(EntityTypeBuilder<PrescriptionItem> builder)
    {
        builder.ToTable("PrescriptionItems");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.MedicineName).IsRequired().HasMaxLength(200);
        builder.Property(x => x.Dosage).HasMaxLength(100);
        builder.Property(x => x.Frequency).HasMaxLength(100);
        builder.Property(x => x.Instructions).HasMaxLength(500);

        builder.HasOne(x => x.Prescription)
               .WithMany(p => p.Items)
               .HasForeignKey(x => x.PrescriptionId)
               .OnDelete(DeleteBehavior.Cascade); // items die with the prescription

        builder.HasIndex(x => x.PrescriptionId);
    }
}
