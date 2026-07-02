using Clinic.Application.Common.Exceptions;
using Clinic.Application.Common.Interfaces;
using Clinic.Application.Common.Models;
using Clinic.Application.Features.Appointments.DTOs;
using Clinic.Application.Features.Appointments.Services;
using Clinic.Domain.Constants;
using Clinic.Domain.Entities;
using Clinic.Domain.Enums;
using Clinic.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Clinic.Infrastructure.Services;

public class AppointmentService : IAppointmentService
{
    private readonly ClinicDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public AppointmentService(ClinicDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<AppointmentDto> CreateAppointmentAsync(
        CreateAppointmentRequest request,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _currentUser.TenantId;
        var createdByTenantUserId = _currentUser.TenantUserId;

        // Verify patient belongs to this clinic (name is used in the notification)
        var patient = await _context.Patients
            .Where(p => p.Id == request.PatientId && p.TenantId == tenantId)
            .Select(p => new { p.FirstName, p.LastName })
            .FirstOrDefaultAsync(cancellationToken);

        if (patient is null)
            throw new BadRequestException("Patient not found.");

        // Verify doctor belongs to this clinic
        var doctorExists = await _context.TenantUsers
            .AnyAsync(tu => tu.Id == request.DoctorTenantUserId
                && tu.TenantId == tenantId
                && tu.IsActive, cancellationToken);

        if (!doctorExists)
            throw new BadRequestException("Doctor not found.");

        var appointment = new Appointment(
            tenantId,
            request.PatientId,
            request.DoctorTenantUserId,
            createdByTenantUserId,
            request.AppointmentDate.ToUniversalTime(),
            request.Notes);

        _context.Appointments.Add(appointment);

        // Tell the doctor — unless they booked it themselves
        if (request.DoctorTenantUserId != createdByTenantUserId)
        {
            _context.Notifications.Add(new Notification(
                tenantId,
                request.DoctorTenantUserId,
                NotificationTypes.Booking,
                "New appointment booked",
                $"{patient.FirstName} {patient.LastName} · {appointment.AppointmentDate:d MMM, HH:mm}"));
        }

        await _context.SaveChangesAsync(cancellationToken);

        // Return full DTO with names
        return await GetAppointmentByIdAsync(appointment.Id, cancellationToken);
    }

    public async Task<PagedResult<AppointmentDto>> GetAppointmentsAsync(
        DateTime? date,
        string? status,
        Guid? doctorTenantUserId,
        PageRequest page,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _currentUser.TenantId;

        var query = _context.Appointments
            .AsNoTracking()
            .Where(a => a.TenantId == tenantId);

        // Filter by date if provided
        if (date.HasValue)
        {
            var dayStart = date.Value.Date.ToUniversalTime();
            var dayEnd = dayStart.AddDays(1);
            query = query.Where(a =>
                a.AppointmentDate >= dayStart &&
                a.AppointmentDate < dayEnd);
        }

        // Filter by status if provided
        if (!string.IsNullOrWhiteSpace(status) &&
            Enum.TryParse<AppointmentStatus>(status, true, out var parsedStatus))
        {
            query = query.Where(a => a.Status == parsedStatus);
        }

        // Filter by doctor if provided
        if (doctorTenantUserId.HasValue)
        {
            query = query.Where(a =>
                a.DoctorTenantUserId == doctorTenantUserId.Value);
        }

        return await query
            .OrderBy(a => a.AppointmentDate)
            .Select(a => new AppointmentDto
            {
                Id = a.Id,
                PatientId = a.PatientId,
                PatientName = a.Patient.FirstName + " " + a.Patient.LastName,
                PatientPhone = a.Patient.Phone,
                DoctorTenantUserId = a.DoctorTenantUserId,
                DoctorName = a.Doctor.SystemUser.FirstName + " " +
                             a.Doctor.SystemUser.LastName,
                AppointmentDate = a.AppointmentDate,
                Status = a.Status.ToString(),
                Notes = a.Notes,
                CreatedAt = a.CreatedAt
            })
            .ToPagedResultAsync(page, cancellationToken);
    }

    public async Task<AppointmentDto> GetAppointmentByIdAsync(
        Guid appointmentId,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _currentUser.TenantId;

        var appointment = await _context.Appointments
            .AsNoTracking()
            .Where(a => a.Id == appointmentId && a.TenantId == tenantId)
            .Select(a => new AppointmentDto
            {
                Id = a.Id,
                PatientId = a.PatientId,
                PatientName = a.Patient.FirstName + " " + a.Patient.LastName,
                PatientPhone = a.Patient.Phone,
                DoctorTenantUserId = a.DoctorTenantUserId,
                DoctorName = a.Doctor.SystemUser.FirstName + " " +
                             a.Doctor.SystemUser.LastName,
                AppointmentDate = a.AppointmentDate,
                Status = a.Status.ToString(),
                Notes = a.Notes,
                CreatedAt = a.CreatedAt
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (appointment is null)
            throw new NotFoundException("Appointment not found.");

        return appointment;
    }

    public async Task<AppointmentDto> UpdateStatusAsync(
        Guid appointmentId,
        UpdateAppointmentStatusRequest request,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _currentUser.TenantId;

        // AsTracking because we are updating
        var appointment = await _context.Appointments
            .FirstOrDefaultAsync(a =>
                a.Id == appointmentId &&
                a.TenantId == tenantId, cancellationToken);

        if (appointment is null)
            throw new NotFoundException("Appointment not found.");

        if (!Enum.TryParse<AppointmentStatus>(request.Status, true, out var newStatus)
            || !Enum.IsDefined(newStatus))   // TryParse accepts raw numbers like "99"
            throw new BadRequestException("Invalid status value.");

        appointment.UpdateStatus(newStatus);

        // Front desk checked the patient in — ping the doctor's bell
        if (newStatus == AppointmentStatus.CheckedIn
            && appointment.DoctorTenantUserId != _currentUser.TenantUserId)
        {
            var patientName = await _context.Patients
                .Where(p => p.Id == appointment.PatientId)
                .Select(p => p.FirstName + " " + p.LastName)
                .FirstAsync(cancellationToken);

            _context.Notifications.Add(new Notification(
                tenantId,
                appointment.DoctorTenantUserId,
                NotificationTypes.CheckIn,
                "Patient arrived",
                $"{patientName} is in the waiting room"));
        }

        await _context.SaveChangesAsync(cancellationToken);

        return await GetAppointmentByIdAsync(appointmentId, cancellationToken);
    }
}