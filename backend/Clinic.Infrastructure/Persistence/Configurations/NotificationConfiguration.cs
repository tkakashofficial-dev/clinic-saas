using Clinic.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Clinic.Infrastructure.Persistence.Configurations;

public class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> builder)
    {
        builder.ToTable("Notifications");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Type).IsRequired().HasMaxLength(30);
        builder.Property(x => x.Title).IsRequired().HasMaxLength(200);
        builder.Property(x => x.Message).IsRequired().HasMaxLength(500);

        builder.HasOne(x => x.Recipient)
               .WithMany()
               .HasForeignKey(x => x.RecipientTenantUserId)
               .OnDelete(DeleteBehavior.Cascade);

        // The hot query: "my unread notifications" — indexed exactly for it
        builder.HasIndex(x => new { x.TenantId, x.RecipientTenantUserId, x.IsRead });
    }
}
