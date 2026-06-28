using Clinic.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Clinic.Infrastructure.Persistence.Configurations;

public class AppointmentConfiguration : IEntityTypeConfiguration<Appointment>
{
    public void Configure(EntityTypeBuilder<Appointment> builder)
    {
        builder.ToTable("Appointments");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Status)
               .HasConversion<string>()
               .HasMaxLength(20)
               .IsRequired();

        builder.Property(x => x.Notes).HasMaxLength(1000);
        builder.Property(x => x.AppointmentDate).IsRequired();

        builder.HasOne(x => x.Tenant)
               .WithMany()
               .HasForeignKey(x => x.TenantId)
               .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Patient)
               .WithMany()
               .HasForeignKey(x => x.PatientId)
               .OnDelete(DeleteBehavior.Restrict);

        // Doctor relationship
        builder.HasOne(x => x.Doctor)
               .WithMany()
               .HasForeignKey(x => x.DoctorTenantUserId)
               .OnDelete(DeleteBehavior.Restrict);

        // Who created this appointment
        builder.HasOne(x => x.CreatedBy)
               .WithMany()
               .HasForeignKey(x => x.CreatedByTenantUserId)
               .OnDelete(DeleteBehavior.Restrict);

        // Index for fast queries by doctor and date
        builder.HasIndex(x => new { x.TenantId, x.AppointmentDate });
        builder.HasIndex(x => new { x.DoctorTenantUserId, x.AppointmentDate });
    }
}