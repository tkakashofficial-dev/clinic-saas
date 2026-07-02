using Clinic.Application.Common.Exceptions;
using Clinic.Application.Common.Interfaces;
using Clinic.Application.Features.Consultations.DTOs;
using Clinic.Application.Features.Consultations.Services;
using Clinic.Domain.Entities;
using Clinic.Domain.Enums;
using Clinic.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Clinic.Infrastructure.Services;

public class ConsultationService : IConsultationService
{
    private readonly ClinicDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public ConsultationService(ClinicDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<ConsultationDto> RecordAsync(
        Guid appointmentId,
        RecordConsultationRequest request,
        CancellationToken cancellationToken = default)
    {
        // Tenant query filter guarantees this can only find OUR appointment
        var appointment = await _context.Appointments
            .FirstOrDefaultAsync(a => a.Id == appointmentId, cancellationToken)
            ?? throw new NotFoundException("Appointment not found.");

        if (appointment.Status == AppointmentStatus.Cancelled)
            throw new BadRequestException("Cannot record a consultation for a cancelled appointment.");

        var alreadyRecorded = await _context.Consultations
            .AnyAsync(c => c.AppointmentId == appointmentId, cancellationToken);
        if (alreadyRecorded)
            throw new ConflictException("A consultation was already recorded for this appointment.");

        var consultation = new Consultation(
            _currentUser.TenantId,
            appointmentId,
            _currentUser.TenantUserId,   // the doctor recording it
            request.Diagnosis,
            request.TreatmentNotes);
        _context.Consultations.Add(consultation);

        if (request.Prescription is not null)
        {
            var prescription = new Prescription(
                _currentUser.TenantId, consultation.Id, request.Prescription.Notes);

            foreach (var item in request.Prescription.Items)
            {
                prescription.AddItem(new PrescriptionItem(
                    prescription.Id,
                    item.MedicineName,
                    item.Dosage,
                    item.Frequency,
                    item.DurationDays,
                    item.Instructions));
            }

            _context.Prescriptions.Add(prescription);
        }

        // The clinical outcome exists — the booking is complete
        appointment.UpdateStatus(AppointmentStatus.Completed);

        await _context.SaveChangesAsync(cancellationToken);

        return await GetByAppointmentAsync(appointmentId, cancellationToken);
    }

    public async Task<ConsultationDto> GetByAppointmentAsync(
        Guid appointmentId,
        CancellationToken cancellationToken = default)
    {
        var consultation = await _context.Consultations
            .AsNoTracking()
            .Where(c => c.AppointmentId == appointmentId)
            .Select(c => new ConsultationDto
            {
                Id = c.Id,
                AppointmentId = c.AppointmentId,
                Diagnosis = c.Diagnosis,
                TreatmentNotes = c.TreatmentNotes,
                DoctorName = c.Doctor.SystemUser.FirstName + " " + c.Doctor.SystemUser.LastName,
                RecordedAt = c.CreatedAt,
                Prescription = c.Prescription == null ? null : new PrescriptionDto
                {
                    Id = c.Prescription.Id,
                    Notes = c.Prescription.Notes,
                    Items = c.Prescription.Items
                        .Select(i => new PrescriptionItemDto
                        {
                            MedicineName = i.MedicineName,
                            Dosage = i.Dosage,
                            Frequency = i.Frequency,
                            DurationDays = i.DurationDays,
                            Instructions = i.Instructions
                        })
                        .ToList()
                }
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (consultation is null)
            throw new NotFoundException("No consultation recorded for this appointment.");

        return consultation;
    }

    public async Task<(byte[] Content, string FileName)> GetPrescriptionPdfAsync(
        Guid prescriptionId,
        CancellationToken cancellationToken = default)
    {
        var data = await _context.Prescriptions
            .AsNoTracking()
            .Where(p => p.Id == prescriptionId)
            .Select(p => new PrescriptionPdfData
            {
                ClinicName = p.Consultation.Appointment.Tenant.Name,
                PatientName = p.Consultation.Appointment.Patient.FirstName + " " +
                              p.Consultation.Appointment.Patient.LastName,
                PatientPhone = p.Consultation.Appointment.Patient.Phone,
                PatientDateOfBirth = p.Consultation.Appointment.Patient.DateOfBirth,
                DoctorName = p.Consultation.Doctor.SystemUser.FirstName + " " +
                             p.Consultation.Doctor.SystemUser.LastName,
                Diagnosis = p.Consultation.Diagnosis,
                Notes = p.Notes,
                IssuedAt = p.CreatedAt,
                Items = p.Items.Select(i => new PrescriptionPdfItem
                {
                    MedicineName = i.MedicineName,
                    Dosage = i.Dosage,
                    Frequency = i.Frequency,
                    DurationDays = i.DurationDays,
                    Instructions = i.Instructions
                }).ToList()
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (data is null)
            throw new NotFoundException("Prescription not found.");

        var pdf = PrescriptionPdfGenerator.Generate(data);
        var fileName = $"prescription-{data.IssuedAt:yyyyMMdd}-{prescriptionId.ToString()[..8]}.pdf";
        return (pdf, fileName);
    }
}
