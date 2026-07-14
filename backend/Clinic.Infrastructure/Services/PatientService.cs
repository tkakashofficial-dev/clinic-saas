using Clinic.Application.Common.Exceptions;
using Clinic.Application.Common.Interfaces;
using Clinic.Application.Common.Models;
using Clinic.Application.Features.Patients.DTOs;
using Clinic.Application.Features.Patients.Services;
using Clinic.Domain.Entities;
using Clinic.Domain.Enums;
using Clinic.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Clinic.Infrastructure.Services;

public class PatientService : IPatientService
{
    private readonly ClinicDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public PatientService(ClinicDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<PatientDto> RegisterPatientAsync(
        RegisterPatientRequest request,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _currentUser.TenantId;
        var tenantUserId = _currentUser.TenantUserId;

        // 1. Check phone not already registered in this clinic
        var phoneExists = await _context.Patients
            .AnyAsync(p => p.TenantId == tenantId
                && p.Phone == request.Phone, cancellationToken);

        if (phoneExists)
            throw new ConflictException(
                "A patient with this phone number already exists.");

        // 2. Parse gender (IsDefined: TryParse accepts raw numbers like "9")
        if (!Enum.TryParse<Gender>(request.Gender, true, out var gender)
            || !Enum.IsDefined(gender))
            throw new BadRequestException("Invalid gender value.");

        // 3. Create patient with the next per-clinic number (unique index
        //    (TenantId, PatientNumber) is the safety net against races)
        var nextNumber = await _context.Patients
            .Where(p => p.TenantId == tenantId)
            .Select(p => (int?)p.PatientNumber)
            .MaxAsync(cancellationToken) ?? 0;

        var patient = new Patient(
            tenantId,
            tenantUserId,
            request.FirstName,
            request.LastName,
            request.Phone,
            gender,
            request.DateOfBirth,
            request.Email,
            request.Address);
        patient.AssignNumber(nextNumber + 1);

        _context.Patients.Add(patient);

        // 4. Attach medical conditions if any
        if (request.MedicalConditionCodes.Any())
        {
            var conditions = await _context.MedicalConditions
                .Where(mc => request.MedicalConditionCodes.Contains(mc.Code))
                .ToListAsync(cancellationToken);

            foreach (var condition in conditions)
            {
                var patientCondition = new PatientMedicalCondition(
                    patient.Id, condition.Id);
                _context.PatientMedicalConditions.Add(patientCondition);
            }
        }

        await _context.SaveChangesAsync(cancellationToken);

        return MapToDto(patient, request.MedicalConditionCodes);
    }

    public async Task<PatientDto> UpdatePatientAsync(
        Guid patientId,
        UpdatePatientRequest request,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _currentUser.TenantId;

        // Tracked load — we're updating (tenant filter scopes this too)
        var patient = await _context.Patients
            .FirstOrDefaultAsync(p => p.Id == patientId && p.TenantId == tenantId, cancellationToken)
            ?? throw new NotFoundException("Patient not found.");

        // Phone must stay unique per clinic — excluding this patient themselves
        var phoneTaken = await _context.Patients
            .AnyAsync(p => p.TenantId == tenantId
                && p.Phone == request.Phone
                && p.Id != patientId, cancellationToken);
        if (phoneTaken)
            throw new ConflictException("Another patient already has this phone number.");

        if (!Enum.TryParse<Gender>(request.Gender, true, out var gender)
            || !Enum.IsDefined(gender))
            throw new BadRequestException("Invalid gender value.");

        patient.Update(
            request.FirstName,
            request.LastName,
            request.Phone,
            gender,
            request.DateOfBirth,
            request.Email,
            request.Address);

        await _context.SaveChangesAsync(cancellationToken);

        return await GetPatientByIdAsync(patientId, cancellationToken);
    }

    public async Task<PagedResult<PatientDto>> GetAllPatientsAsync(
        string? search,
        PageRequest page,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _currentUser.TenantId;

        var query = _context.Patients
            .AsNoTracking()
            .Where(p => p.TenantId == tenantId);

        // Search by name or phone
        if (!string.IsNullOrWhiteSpace(search))
        {
            search = search.ToLower();
            query = query.Where(p =>
                p.FirstName.ToLower().Contains(search) ||
                p.LastName.ToLower().Contains(search) ||
                p.Phone.Contains(search));
        }

        return await query
            .OrderByDescending(p => p.CreatedAt)   // stable order BEFORE Skip/Take
            .Select(p => new PatientDto
            {
                Id = p.Id,
                FullName = p.FirstName + " " + p.LastName,
                PatientNumber = p.PatientNumber,
                FirstName = p.FirstName,
                LastName = p.LastName,
                Phone = p.Phone,
                Email = p.Email,
                Address = p.Address,
                Gender = p.Gender.ToString(),
                DateOfBirth = p.DateOfBirth,
                Age = CalculateAge(p.DateOfBirth),
                MedicalConditions = p.MedicalConditions
                    .Select(mc => mc.MedicalCondition.Name)
                    .ToList(),
                RegisteredAt = p.CreatedAt
            })
            .ToPagedResultAsync(page, cancellationToken);
    }

    public async Task<PatientDto> GetPatientByIdAsync(
        Guid patientId,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _currentUser.TenantId;

        var patient = await _context.Patients
            .AsNoTracking()
            .Where(p => p.Id == patientId && p.TenantId == tenantId)
            .Select(p => new PatientDto
            {
                Id = p.Id,
                FullName = p.FirstName + " " + p.LastName,
                PatientNumber = p.PatientNumber,
                FirstName = p.FirstName,
                LastName = p.LastName,
                Phone = p.Phone,
                Email = p.Email,
                Address = p.Address,
                Gender = p.Gender.ToString(),
                DateOfBirth = p.DateOfBirth,
                Age = CalculateAge(p.DateOfBirth),
                MedicalConditions = p.MedicalConditions
                    .Select(mc => mc.MedicalCondition.Name)
                    .ToList(),
                RegisteredAt = p.CreatedAt
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (patient is null)
            throw new NotFoundException("Patient not found.");

        return patient;
    }

    public async Task<PatientHistoryDto> GetHistoryAsync(
        Guid patientId,
        CancellationToken cancellationToken = default)
    {
        var patient = await GetPatientByIdAsync(patientId, cancellationToken);

        var consultations = await _context.Consultations
            .AsNoTracking()
            .Where(c => c.Appointment.PatientId == patientId)
            .OrderByDescending(c => c.Appointment.AppointmentDate)
            .Select(c => new PatientConsultationDto
            {
                ConsultationId = c.Id,
                AppointmentDate = c.Appointment.AppointmentDate,
                RecordedAt = c.CreatedAt,
                DoctorName = c.Doctor.SystemUser.FirstName + " " + c.Doctor.SystemUser.LastName,
                Diagnosis = c.Diagnosis,
                TreatmentNotes = c.TreatmentNotes,
                BloodPressure = c.BloodPressure,
                PulseBpm = c.PulseBpm,
                TemperatureCelsius = c.TemperatureCelsius,
                WeightKg = c.WeightKg,
                PrescriptionId = c.Prescription == null ? null : c.Prescription.Id
            })
            .ToListAsync(cancellationToken);

        return new PatientHistoryDto { Patient = patient, Consultations = consultations };
    }

    public async Task<(byte[] Content, string FileName)> GetIntakeFormPdfAsync(
        Guid patientId,
        CancellationToken cancellationToken = default)
    {
        var patient = await GetPatientByIdAsync(patientId, cancellationToken);

        var tenant = await _context.Tenants
            .AsNoTracking()
            .FirstAsync(t => t.Id == _currentUser.TenantId, cancellationToken);

        var pdf = IntakeFormPdfGenerator.Generate(
            tenant.Name, tenant.Address, tenant.Phone, patient);
        return (pdf, $"intake-P{patient.PatientNumber:D6}.pdf");
    }

    private static PatientDto MapToDto(Patient patient, List<string> conditionCodes)
    {
        return new PatientDto
        {
            Id = patient.Id,
            FullName = $"{patient.FirstName} {patient.LastName}",
            PatientNumber = patient.PatientNumber,
            FirstName = patient.FirstName,
            LastName = patient.LastName,
            Phone = patient.Phone,
            Email = patient.Email,
            Address = patient.Address,
            Gender = patient.Gender.ToString(),
            DateOfBirth = patient.DateOfBirth,
            Age = CalculateAge(patient.DateOfBirth),
            MedicalConditions = conditionCodes,
            RegisteredAt = patient.CreatedAt
        };
    }

    private static int? CalculateAge(DateOnly? dateOfBirth)
    {
        if (!dateOfBirth.HasValue)
            return null;

        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var age = today.Year - dateOfBirth.Value.Year;

        if (dateOfBirth.Value > today.AddYears(-age))
            age--;

        return age;
    }
}