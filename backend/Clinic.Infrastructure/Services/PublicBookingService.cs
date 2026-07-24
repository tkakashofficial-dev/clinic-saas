using System.Text.RegularExpressions;
using Clinic.Application.Common.Exceptions;
using Clinic.Application.Features.PublicBooking.DTOs;
using Clinic.Application.Features.PublicBooking.Services;
using Clinic.Domain.Constants;
using Clinic.Domain.Entities;
using Clinic.Domain.Enums;
using Clinic.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Clinic.Infrastructure.Services;

/// <summary>
/// Anonymous booking (/book/{slug}). There is NO current tenant here, so the
/// tenant query filter matches nothing — every read must IgnoreQueryFilters
/// and pin TenantId explicitly. Writes carry an explicit TenantId, which the
/// write guard allows for unauthenticated requests (the registration path).
/// </summary>
public class PublicBookingService : IPublicBookingService
{
    private readonly ClinicDbContext _context;

    public PublicBookingService(ClinicDbContext context)
    {
        _context = context;
    }

    public async Task<PublicClinicDto> GetClinicAsync(
        string slug, CancellationToken cancellationToken = default)
    {
        var tenant = await FindBookableClinicAsync(slug, cancellationToken);

        var doctors = await _context.TenantUsers
            .IgnoreQueryFilters([QueryFilters.Tenant])
            .AsNoTracking()
            .Where(tu => tu.TenantId == tenant.Id && tu.IsActive
                && tu.Roles.Any(r => r.Role.Name == RoleNames.Doctor))
            .OrderBy(tu => tu.SystemUser.FirstName)
            .Select(tu => new PublicDoctorDto
            {
                Id = tu.Id,
                Name = "Dr. " + tu.SystemUser.FirstName + " " + tu.SystemUser.LastName,
            })
            .ToListAsync(cancellationToken);

        return new PublicClinicDto
        {
            Name = tenant.Name,
            Address = tenant.Address,
            Phone = tenant.Phone,
            Doctors = doctors,
        };
    }

    public async Task<PublicBookingResultDto> BookAsync(
        string slug, PublicBookingRequest request,
        CancellationToken cancellationToken = default)
    {
        var tenant = await FindBookableClinicAsync(slug, cancellationToken);

        // Honeypot: bots fill every field. Pretend success, write nothing.
        if (!string.IsNullOrEmpty(request.Website))
            return new PublicBookingResultDto
            {
                ClinicName = tenant.Name,
                DoctorName = "",
                AppointmentAt = request.AppointmentAt,
            };

        var name = (request.PatientName ?? "").Trim();
        if (name.Length is < 2 or > 120)
            throw new BadRequestException("Please enter your full name.");

        var phone = (request.Phone ?? "").Trim();
        if (!Regex.IsMatch(phone, @"^\+?[0-9 ()\-]{7,20}$"))
            throw new BadRequestException("Please enter a valid phone number.");

        var now = DateTime.UtcNow;
        if (request.AppointmentAt < now.AddMinutes(15))
            throw new BadRequestException("Please pick a time at least a little in the future.");
        if (request.AppointmentAt > now.AddDays(60))
            throw new BadRequestException("Online booking is open for the next 60 days.");

        var doctor = await _context.TenantUsers
            .IgnoreQueryFilters([QueryFilters.Tenant])
            .AsNoTracking()
            .Where(tu => tu.Id == request.DoctorId && tu.TenantId == tenant.Id
                && tu.IsActive && tu.Roles.Any(r => r.Role.Name == RoleNames.Doctor))
            .Select(tu => new
            {
                tu.Id,
                Name = "Dr. " + tu.SystemUser.FirstName + " " + tu.SystemUser.LastName,
            })
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("That doctor is not available for booking.");

        // Find or register the patient by phone. IgnoreQueryFilters() (both
        // filters) on purpose: a soft-deleted patient still owns the phone in
        // the unique index, so an insert would explode — handle it politely.
        var patient = await _context.Patients
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(p => p.TenantId == tenant.Id && p.Phone == phone,
                cancellationToken);

        if (patient is { IsDeleted: true })
            throw new BadRequestException(
                "This phone number needs attention — please call the clinic to book.");

        if (patient is not null)
        {
            // One online booking per day per patient — stops fat-finger dupes
            var dayStart = request.AppointmentAt.Date;
            var alreadyBooked = await _context.Appointments
                .IgnoreQueryFilters([QueryFilters.Tenant])
                .AnyAsync(a => a.TenantId == tenant.Id
                    && a.PatientId == patient.Id
                    && a.Status == AppointmentStatus.Scheduled
                    && a.AppointmentDate >= dayStart
                    && a.AppointmentDate < dayStart.AddDays(1), cancellationToken);

            if (alreadyBooked)
                throw new ConflictException(
                    "You already have a booking that day — call the clinic to change it.");
        }
        else
        {
            var spaceIndex = name.IndexOf(' ');
            var firstName = spaceIndex < 0 ? name : name[..spaceIndex];
            var lastName = spaceIndex < 0 ? "" : name[(spaceIndex + 1)..].Trim();

            // Include soft-deleted rows in the number sequence — the unique
            // index counts them too
            var nextNumber = await _context.Patients
                .IgnoreQueryFilters()
                .Where(p => p.TenantId == tenant.Id)
                .Select(p => (int?)p.PatientNumber)
                .MaxAsync(cancellationToken) ?? 0;

            patient = new Patient(
                tenant.Id,
                doctor.Id,               // "registered via Dr. X's booking page"
                firstName,
                lastName,
                phone,
                Gender.Other,            // reception completes the record on arrival
                dateOfBirth: null);
            patient.AssignNumber(nextNumber + 1);
            _context.Patients.Add(patient);
        }

        var note = (request.Note ?? "").Trim();
        if (note.Length > 500) note = note[..500];

        var appointment = new Appointment(
            tenant.Id,
            patient.Id,
            doctor.Id,
            doctor.Id,                   // no staff member did this — attribute to the doctor
            DateTime.SpecifyKind(request.AppointmentAt, DateTimeKind.Utc),
            note.Length == 0 ? "[Booked online]" : $"[Booked online] {note}");

        _context.Appointments.Add(appointment);

        // Ring the bells: the doctor + every admin should see this instantly
        var recipients = await _context.TenantUsers
            .IgnoreQueryFilters([QueryFilters.Tenant])
            .Where(tu => tu.TenantId == tenant.Id && tu.IsActive
                && (tu.Id == doctor.Id
                    || tu.Roles.Any(r => r.Role.Name == RoleNames.Admin)))
            .Select(tu => tu.Id)
            .ToListAsync(cancellationToken);

        foreach (var recipientId in recipients.Distinct())
            _context.Notifications.Add(new Notification(
                tenant.Id,
                recipientId,
                NotificationTypes.Booking,
                "New online booking",
                $"{name} · {appointment.AppointmentDate:d MMM, HH:mm} · booked from the public page"));

        await _context.SaveChangesAsync(cancellationToken);

        return new PublicBookingResultDto
        {
            ClinicName = tenant.Name,
            DoctorName = doctor.Name,
            AppointmentAt = appointment.AppointmentDate,
        };
    }

    private async Task<Tenant> FindBookableClinicAsync(string slug, CancellationToken ct)
    {
        slug = (slug ?? "").Trim().ToLowerInvariant();
        if (slug.Length == 0) throw new NotFoundException("Clinic not found.");

        return await _context.Tenants
                   .AsNoTracking()
                   .FirstOrDefaultAsync(t => t.Slug == slug && t.IsActive
                       && t.PublicBookingEnabled, ct)
               ?? throw new NotFoundException(
                   "This clinic's booking page is not available.");
    }
}
