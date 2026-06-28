using Clinic.Application.Common.Interfaces;
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
            throw new InvalidOperationException(
                "A patient with this phone number already exists.");

        // 2. Parse gender
        if (!Enum.TryParse<Gender>(request.Gender, true, out var gender))
            throw new InvalidOperationException("Invalid gender value.");

        // 3. Create patient
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

    public async Task<List<PatientDto>> GetAllPatientsAsync(
        string? search,
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
            .Select(p => new PatientDto
            {
                Id = p.Id,
                FullName = p.FirstName + " " + p.LastName,
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
            .OrderByDescending(p => p.RegisteredAt)
            .ToListAsync(cancellationToken);
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
            throw new KeyNotFoundException("Patient not found.");

        return patient;
    }

    private static PatientDto MapToDto(Patient patient, List<string> conditionCodes)
    {
        return new PatientDto
        {
            Id = patient.Id,
            FullName = $"{patient.FirstName} {patient.LastName}",
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