using Clinic.Application.Common.Exceptions;
using Clinic.Application.Features.Consultations.DTOs;
using Clinic.Domain.Entities;
using Clinic.Domain.Enums;
using Clinic.Infrastructure.Services;
using Clinic.Tests.TestInfrastructure;
using Microsoft.EntityFrameworkCore;

namespace Clinic.Tests;

public class ConsultationServiceTests : IDisposable
{
    private readonly TestDb _db = new();

    private ConsultationService CreateService() =>
        new(_db.CreateContext(), _db.CurrentUser);

    private async Task<Guid> SeedAppointmentAsync(
        Guid tenantId, Guid tenantUserId, AppointmentStatus status = AppointmentStatus.Scheduled)
    {
        _db.CurrentUser.ActAs(tenantId, tenantUserId);
        await using var context = _db.CreateContext();

        var patient = new Patient(
            tenantId, tenantUserId, "Pat", "Ient",
            $"+31{Random.Shared.Next(100000000, 999999999)}", Gender.Other, null);
        context.Patients.Add(patient);

        var appointment = new Appointment(
            tenantId, patient.Id, tenantUserId, tenantUserId,
            DateTime.UtcNow.AddHours(1));
        if (status != AppointmentStatus.Scheduled)
            appointment.UpdateStatus(status);
        context.Appointments.Add(appointment);

        await context.SaveChangesAsync();
        return appointment.Id;
    }

    private static RecordConsultationRequest RequestWithPrescription() => new()
    {
        Diagnosis = "Caries on molar 36",
        TreatmentNotes = "Filling placed",
        Prescription = new PrescriptionRequest
        {
            Notes = "Take after meals",
            Items =
            [
                new PrescriptionItemRequest
                {
                    MedicineName = "Amoxicillin",
                    Dosage = "500 mg",
                    Frequency = "3x daily",
                    DurationDays = 5
                }
            ]
        }
    };

    [Fact]
    public async Task RecordConsultation_CompletesAppointment_AndStoresPrescription()
    {
        var clinic = await _db.SeedTenantAsync("Clinic", "a@a.com");
        var appointmentId = await SeedAppointmentAsync(clinic.TenantId, clinic.TenantUserId);

        var dto = await CreateService().RecordAsync(appointmentId, RequestWithPrescription());

        Assert.Equal("Caries on molar 36", dto.Diagnosis);
        Assert.NotNull(dto.Prescription);
        Assert.Single(dto.Prescription!.Items);
        Assert.Equal("Amoxicillin", dto.Prescription.Items[0].MedicineName);

        await using var context = _db.CreateContext();
        var appointment = await context.Appointments.SingleAsync(a => a.Id == appointmentId);
        Assert.Equal(AppointmentStatus.Completed, appointment.Status);
    }

    [Fact]
    public async Task RecordConsultation_Twice_ThrowsConflict()
    {
        var clinic = await _db.SeedTenantAsync("Clinic", "a@a.com");
        var appointmentId = await SeedAppointmentAsync(clinic.TenantId, clinic.TenantUserId);

        await CreateService().RecordAsync(appointmentId, RequestWithPrescription());

        await Assert.ThrowsAsync<ConflictException>(
            () => CreateService().RecordAsync(appointmentId, RequestWithPrescription()));
    }

    [Fact]
    public async Task RecordConsultation_CancelledAppointment_ThrowsBadRequest()
    {
        var clinic = await _db.SeedTenantAsync("Clinic", "a@a.com");
        var appointmentId = await SeedAppointmentAsync(
            clinic.TenantId, clinic.TenantUserId, AppointmentStatus.Cancelled);

        await Assert.ThrowsAsync<BadRequestException>(
            () => CreateService().RecordAsync(appointmentId, RequestWithPrescription()));
    }

    [Fact]
    public async Task RecordConsultation_OtherTenantsAppointment_ThrowsNotFound()
    {
        var clinicA = await _db.SeedTenantAsync("Clinic A", "a@a.com");
        var clinicB = await _db.SeedTenantAsync("Clinic B", "b@b.com");
        var appointmentId = await SeedAppointmentAsync(clinicA.TenantId, clinicA.TenantUserId);

        // Doctor from clinic B tries to record on clinic A's appointment
        _db.CurrentUser.ActAs(clinicB.TenantId, clinicB.TenantUserId);
        await Assert.ThrowsAsync<NotFoundException>(
            () => CreateService().RecordAsync(appointmentId, RequestWithPrescription()));
    }

    [Fact]
    public async Task PatientHistory_ContainsRecordedConsultation()
    {
        var clinic = await _db.SeedTenantAsync("Clinic", "a@a.com");
        var appointmentId = await SeedAppointmentAsync(clinic.TenantId, clinic.TenantUserId);
        await CreateService().RecordAsync(appointmentId, RequestWithPrescription());

        await using var context = _db.CreateContext();
        var patientId = (await context.Appointments.SingleAsync(a => a.Id == appointmentId)).PatientId;

        var patientService = new PatientService(_db.CreateContext(), _db.CurrentUser);
        var history = await patientService.GetHistoryAsync(patientId);

        Assert.Single(history.Consultations);
        Assert.Equal("Caries on molar 36", history.Consultations[0].Diagnosis);
        Assert.NotNull(history.Consultations[0].PrescriptionId);
    }

    [Fact]
    public async Task PrescriptionPdf_GeneratesNonEmptyPdfBytes()
    {
        var clinic = await _db.SeedTenantAsync("Clinic", "a@a.com");
        var appointmentId = await SeedAppointmentAsync(clinic.TenantId, clinic.TenantUserId);
        var dto = await CreateService().RecordAsync(appointmentId, RequestWithPrescription());

        var (content, fileName) = await CreateService()
            .GetPrescriptionPdfAsync(dto.Prescription!.Id);

        Assert.True(content.Length > 1000, "PDF should have real content");
        Assert.StartsWith("%PDF", System.Text.Encoding.ASCII.GetString(content[..4]));
        Assert.EndsWith(".pdf", fileName);
    }

    public void Dispose() => _db.Dispose();
}
