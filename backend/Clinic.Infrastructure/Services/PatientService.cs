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
            request.Address,
            NormalizeBloodGroup(request.BloodGroup));
        patient.AssignNumber(nextNumber + 1);

        _context.Patients.Add(patient);

        // 4. Attach medical conditions if any
        var conditions = new List<MedicalCondition>();
        if (request.MedicalConditionCodes.Any())
        {
            conditions = await _context.MedicalConditions
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

        return MapToDto(patient, conditions);
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
            request.Address,
            NormalizeBloodGroup(request.BloodGroup));

        // Sync conditions to EXACTLY the submitted set: the edit form always
        // sends every checked code, so missing = unchecked = remove
        var submitted = await _context.MedicalConditions
            .Where(mc => request.MedicalConditionCodes.Contains(mc.Code))
            .Select(mc => mc.Id)
            .ToListAsync(cancellationToken);

        var existing = await _context.PatientMedicalConditions
            .Where(pc => pc.PatientId == patientId)
            .ToListAsync(cancellationToken);

        _context.PatientMedicalConditions.RemoveRange(
            existing.Where(pc => !submitted.Contains(pc.MedicalConditionId)));

        var known = existing.Select(pc => pc.MedicalConditionId).ToHashSet();
        foreach (var conditionId in submitted.Where(id => !known.Contains(id)))
            _context.PatientMedicalConditions.Add(
                new PatientMedicalCondition(patientId, conditionId));

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
                BloodGroup = p.BloodGroup,
                MedicalConditions = p.MedicalConditions
                    .Select(mc => mc.MedicalCondition.Name)
                    .ToList(),
                MedicalConditionCodes = p.MedicalConditions
                    .Select(mc => mc.MedicalCondition.Code)
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
                BloodGroup = p.BloodGroup,
                MedicalConditions = p.MedicalConditions
                    .Select(mc => mc.MedicalCondition.Name)
                    .ToList(),
                MedicalConditionCodes = p.MedicalConditions
                    .Select(mc => mc.MedicalCondition.Code)
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
        string template = "dental",
        CancellationToken cancellationToken = default)
    {
        var patient = await GetPatientByIdAsync(patientId, cancellationToken);

        var tenant = await _context.Tenants
            .AsNoTracking()
            .FirstAsync(t => t.Id == _currentUser.TenantId, cancellationToken);

        // The clinic's own form-builder sections print on extra pages, and
        // digitally-captured answers print PRE-FILLED (when they match the
        // requested template)
        var forms = new FormsService(_context, _currentUser);
        var sections = await forms.GetSectionsForTemplateAsync(template, cancellationToken);
        var latest = await forms.GetLatestResponseAsync(patientId, cancellationToken);
        var answers = latest?.Template == template ? latest.Answers : null;

        var pdf = IntakeFormPdfGenerator.Generate(
            tenant.Name, tenant.Address, tenant.Phone, patient, template, sections, answers);
        return (pdf, $"intake-{template}-P{patient.PatientNumber:D6}.pdf");
    }

    /// <summary>Every clinic shares the same seeded condition list — the
    /// register/edit forms render these as tick-boxes.</summary>
    public async Task<List<MedicalConditionDto>> GetMedicalConditionsAsync(
        CancellationToken cancellationToken = default)
        => await _context.MedicalConditions
            .AsNoTracking()
            .OrderBy(mc => mc.Name)
            .Select(mc => new MedicalConditionDto { Name = mc.Name, Code = mc.Code })
            .ToListAsync(cancellationToken);

    public async Task<string> ExportCsvAsync(CancellationToken cancellationToken = default)
    {
        var patients = await _context.Patients
            .AsNoTracking()
            .OrderBy(p => p.PatientNumber)
            .Select(p => new
            {
                p.PatientNumber,
                p.FirstName,
                p.LastName,
                p.Phone,
                p.Email,
                p.Address,
                Gender = p.Gender.ToString(),
                p.DateOfBirth,
                p.BloodGroup,
                Conditions = p.MedicalConditions
                    .Select(mc => mc.MedicalCondition.Name).ToList(),
                p.CreatedAt,
            })
            .ToListAsync(cancellationToken);

        var sb = new System.Text.StringBuilder();
        sb.AppendLine(CsvUtil.Row(
            "PatientNumber", "FirstName", "LastName", "Phone", "Email", "Address",
            "Gender", "DateOfBirth", "BloodGroup", "MedicalConditions", "RegisteredAt"));

        foreach (var p in patients)
            sb.AppendLine(CsvUtil.Row(
                $"P-{p.PatientNumber:D6}",
                p.FirstName,
                p.LastName,
                p.Phone,
                p.Email,
                p.Address,
                p.Gender,
                p.DateOfBirth?.ToString("yyyy-MM-dd"),
                p.BloodGroup,
                string.Join("; ", p.Conditions),
                p.CreatedAt.ToString("yyyy-MM-dd")));

        return sb.ToString();
    }

    public async Task<ImportResultDto> ImportCsvAsync(
        string csvText, CancellationToken cancellationToken = default)
    {
        var rows = CsvUtil.Parse(csvText);
        if (rows.Count < 2)
            throw new BadRequestException(
                "The file needs a header row plus at least one patient row.");
        if (rows.Count > 2001)
            throw new BadRequestException(
                "Maximum 2,000 patients per import — split larger files.");

        // Header is matched by NAME, not position, so column order is free.
        // "First Name", "first_name" and "FirstName" all match firstname.
        var header = rows[0]
            .Select((h, i) => (Key: NormalizeHeader(h), Index: i))
            .Where(h => h.Key.Length > 0)
            .GroupBy(h => h.Key)
            .ToDictionary(g => g.Key, g => g.First().Index);

        if (!header.ContainsKey("firstname") || !header.ContainsKey("phone"))
            throw new BadRequestException(
                "The header row must contain at least FirstName and Phone columns.");

        string? Cell(string[] row, string key)
        {
            if (!header.TryGetValue(key, out var idx) || idx >= row.Length) return null;
            var value = row[idx].Trim();
            return value.Length == 0 ? null : value;
        }

        var tenantId = _currentUser.TenantId;
        var tenantUserId = _currentUser.TenantUserId;

        var existingPhones = (await _context.Patients
                .Where(p => p.TenantId == tenantId)
                .Select(p => p.Phone)
                .ToListAsync(cancellationToken))
            .ToHashSet();

        var nextNumber = (await _context.Patients
            .Where(p => p.TenantId == tenantId)
            .Select(p => (int?)p.PatientNumber)
            .MaxAsync(cancellationToken) ?? 0) + 1;

        var result = new ImportResultDto();

        for (var r = 1; r < rows.Count; r++)
        {
            var row = rows[r];
            var rowNumber = r + 1;   // 1-based, header included — matches Excel

            var firstName = Cell(row, "firstname");
            var phone = Cell(row, "phone");

            if (firstName is null || phone is null)
            {
                result.Errors.Add(new ImportRowError
                { Row = rowNumber, Message = "Missing first name or phone — row skipped." });
                result.Skipped++;
                continue;
            }
            if (!System.Text.RegularExpressions.Regex.IsMatch(phone, @"^\+?[0-9 ()\-]{7,20}$"))
            {
                result.Errors.Add(new ImportRowError
                { Row = rowNumber, Message = $"'{phone}' is not a valid phone number — row skipped." });
                result.Skipped++;
                continue;
            }
            if (!existingPhones.Add(phone))
            {
                result.Errors.Add(new ImportRowError
                { Row = rowNumber, Message = $"Phone {phone} already exists — row skipped." });
                result.Skipped++;
                continue;
            }

            // Optional fields are best-effort: a bad date or blood group
            // becomes blank rather than blocking the whole migration
            var gender = Enum.TryParse<Gender>(Cell(row, "gender"), true, out var g)
                && Enum.IsDefined(g) ? g : Gender.Other;

            var bloodGroup = NormalizeBloodGroup(Cell(row, "bloodgroup"));
            if (bloodGroup is not null && !Domain.Constants.BloodGroups.IsValid(bloodGroup))
                bloodGroup = null;

            var lastName = Cell(row, "lastname") ?? "";

            var patient = new Patient(
                tenantId,
                tenantUserId,
                Truncate(firstName, 100)!,
                Truncate(lastName, 100)!,
                phone,
                gender,
                ParseFlexibleDate(Cell(row, "dateofbirth")),
                Truncate(Cell(row, "email"), 256),
                Truncate(Cell(row, "address"), 500),
                bloodGroup);
            patient.AssignNumber(nextNumber++);

            _context.Patients.Add(patient);
            result.Imported++;
        }

        if (result.Imported > 0)
            await _context.SaveChangesAsync(cancellationToken);

        return result;
    }

    /// <summary>"First Name" / "first_name" / "FIRSTNAME" → "firstname".</summary>
    private static string NormalizeHeader(string header)
        => new string(header.Where(char.IsLetter).ToArray()).ToLowerInvariant();

    /// <summary>Clips to the DB column limit instead of failing the row.</summary>
    private static string? Truncate(string? value, int max)
        => value is null || value.Length <= max ? value : value[..max];

    /// <summary>Accepts the formats Indian Excel sheets actually contain.</summary>
    private static DateOnly? ParseFlexibleDate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        string[] formats = ["yyyy-MM-dd", "dd-MM-yyyy", "dd/MM/yyyy", "d/M/yyyy", "d-M-yyyy"];
        foreach (var format in formats)
            if (DateOnly.TryParseExact(value.Trim(), format, out var date)
                && date <= DateOnly.FromDateTime(DateTime.UtcNow))
                return date;
        return null;
    }

    /// <summary>Trim + uppercase so "o+" and "O+ " match the canonical set.</summary>
    private static string? NormalizeBloodGroup(string? bloodGroup)
        => string.IsNullOrWhiteSpace(bloodGroup) ? null : bloodGroup.Trim().ToUpperInvariant();

    private static PatientDto MapToDto(Patient patient, List<MedicalCondition> conditions)
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
            BloodGroup = patient.BloodGroup,
            MedicalConditions = conditions.Select(c => c.Name).ToList(),
            MedicalConditionCodes = conditions.Select(c => c.Code).ToList(),
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