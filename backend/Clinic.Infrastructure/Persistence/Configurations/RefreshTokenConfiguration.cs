using Clinic.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Clinic.Infrastructure.Persistence.Configurations;

public class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.ToTable("RefreshTokens");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.TokenHash).IsRequired().HasMaxLength(128);

        builder.HasOne(x => x.SystemUser)
               .WithMany()
               .HasForeignKey(x => x.SystemUserId)
               .OnDelete(DeleteBehavior.Cascade);

        // Refresh lookup happens by hash — must be indexed and unique
        builder.HasIndex(x => x.TokenHash).IsUnique();
        builder.HasIndex(x => x.SystemUserId);
    }
}
