using Clinic.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Clinic.Infrastructure.Persistence.Configurations;

public class PlatformPaymentConfiguration : IEntityTypeConfiguration<PlatformPayment>
{
    public void Configure(EntityTypeBuilder<PlatformPayment> builder)
    {
        builder.ToTable("PlatformPayments");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.AmountRupees).HasPrecision(10, 2);
        builder.Property(x => x.Method).IsRequired().HasMaxLength(20);
        builder.Property(x => x.Note).HasMaxLength(500);
        builder.Property(x => x.RecordedByEmail).IsRequired().HasMaxLength(256);

        builder.HasOne(x => x.Tenant)
               .WithMany()
               .HasForeignKey(x => x.TenantId)
               .OnDelete(DeleteBehavior.Restrict);

        // The console's hot query: latest payment / max coverage per clinic
        builder.HasIndex(x => new { x.TenantId, x.PaidAt });
    }
}
