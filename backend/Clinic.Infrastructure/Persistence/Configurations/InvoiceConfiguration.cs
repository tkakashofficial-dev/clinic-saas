using Clinic.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Clinic.Infrastructure.Persistence.Configurations;

public class InvoiceConfiguration : IEntityTypeConfiguration<Invoice>
{
    public void Configure(EntityTypeBuilder<Invoice> builder)
    {
        builder.ToTable("Invoices");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Status).IsRequired().HasMaxLength(12);
        builder.Property(x => x.SubtotalRupees).HasPrecision(12, 2);
        builder.Property(x => x.DiscountRupees).HasPrecision(12, 2);
        builder.Property(x => x.TotalRupees).HasPrecision(12, 2);
        builder.Property(x => x.PaymentMethod).HasMaxLength(20);
        builder.Property(x => x.Notes).HasMaxLength(300);

        builder.HasOne(x => x.Patient)
               .WithMany()
               .HasForeignKey(x => x.PatientId)
               .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.CreatedByUser)
               .WithMany()
               .HasForeignKey(x => x.CreatedByTenantUserId)
               .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(x => x.Items)
               .WithOne()
               .HasForeignKey(i => i.InvoiceId)
               .OnDelete(DeleteBehavior.Cascade);

        // Per-clinic invoice numbering — the DB is the race-safety net
        builder.HasIndex(x => new { x.TenantId, x.InvoiceNumber }).IsUnique();
        builder.HasIndex(x => new { x.TenantId, x.Status });
    }
}

public class InvoiceItemConfiguration : IEntityTypeConfiguration<InvoiceItem>
{
    public void Configure(EntityTypeBuilder<InvoiceItem> builder)
    {
        builder.ToTable("InvoiceItems");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Description).IsRequired().HasMaxLength(160);
        builder.Property(x => x.UnitPriceRupees).HasPrecision(12, 2);
        builder.Property(x => x.LineTotalRupees).HasPrecision(12, 2);
    }
}
