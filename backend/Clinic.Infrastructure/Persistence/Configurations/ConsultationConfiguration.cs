using Clinic.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Clinic.Infrastructure.Persistence.Configurations;

public class ConsultationConfiguration : IEntityTypeConfiguration<Consultation>
{
    public void Configure(EntityTypeBuilder<Consultation> builder)
    {
        builder.ToTable("Consultations");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Diagnosis).IsRequired().HasMaxLength(2000);
        builder.Property(x => x.TreatmentNotes).HasMaxLength(2000);
        builder.Property(x => x.BloodPressure).HasMaxLength(20);
        builder.Property(x => x.TemperatureCelsius).HasPrecision(4, 1);
        builder.Property(x => x.WeightKg).HasPrecision(5, 1);

        builder.HasOne(x => x.Appointment)
               .WithMany()
               .HasForeignKey(x => x.AppointmentId)
               .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Doctor)
               .WithMany()
               .HasForeignKey(x => x.DoctorTenantUserId)
               .OnDelete(DeleteBehavior.Restrict);

        // One consultation per appointment — enforced by the database
        builder.HasIndex(x => x.AppointmentId).IsUnique();
        builder.HasIndex(x => x.TenantId);
    }
}
