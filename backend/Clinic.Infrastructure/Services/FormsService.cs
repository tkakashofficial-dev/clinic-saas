using Clinic.Application.Common.Exceptions;
using Clinic.Application.Common.Interfaces;
using Clinic.Application.Features.Forms.DTOs;
using Clinic.Application.Features.Forms.Services;
using Clinic.Application.Features.Patients.DTOs;
using Clinic.Domain.Entities;
using Clinic.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace Clinic.Infrastructure.Services;

public class FormsService : IFormsService
{
    private static readonly string[] Kinds = ["box", "lines", "checklist"];
    private static readonly string[] Templates = ["dental", "general", "both"];

    private readonly ClinicDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public FormsService(ClinicDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<List<IntakeFormSectionDto>> GetSectionsAsync(
        CancellationToken cancellationToken = default)
    {
        var sections = await _context.IntakeFormSections
            .AsNoTracking()
            .OrderBy(s => s.SortOrder)
            .ToListAsync(cancellationToken);
        return sections.Select(MapToDto).ToList();
    }

    public async Task<IntakeFormSectionDto> CreateSectionAsync(
        SaveIntakeFormSectionRequest request, CancellationToken cancellationToken = default)
    {
        var (template, kind, title, items) = Validate(request);

        var nextOrder = await _context.IntakeFormSections
            .Select(s => (int?)s.SortOrder)
            .MaxAsync(cancellationToken) ?? 0;

        var section = new IntakeFormSection(
            _currentUser.TenantId, template, kind, title,
            JsonSerializer.Serialize(items), nextOrder + 1);

        _context.IntakeFormSections.Add(section);
        await _context.SaveChangesAsync(cancellationToken);
        return MapToDto(section);
    }

    public async Task<IntakeFormSectionDto> UpdateSectionAsync(
        Guid sectionId, SaveIntakeFormSectionRequest request, CancellationToken cancellationToken = default)
    {
        var (template, kind, title, items) = Validate(request);
        var section = await GetSectionAsync(sectionId, cancellationToken);

        section.Update(template, kind, title, JsonSerializer.Serialize(items));
        await _context.SaveChangesAsync(cancellationToken);
        return MapToDto(section);
    }

    public async Task DeleteSectionAsync(Guid sectionId, CancellationToken cancellationToken = default)
    {
        var section = await GetSectionAsync(sectionId, cancellationToken);
        section.Delete(_currentUser.UserId);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<List<IntakeFormSectionDto>> MoveSectionAsync(
        Guid sectionId, int direction, CancellationToken cancellationToken = default)
    {
        if (direction is not (1 or -1))
            throw new BadRequestException("Direction must be 1 (down) or -1 (up).");

        var sections = await _context.IntakeFormSections
            .OrderBy(s => s.SortOrder)
            .ToListAsync(cancellationToken);

        var index = sections.FindIndex(s => s.Id == sectionId);
        if (index < 0) throw new NotFoundException("Section not found.");

        var target = index + direction;
        if (target >= 0 && target < sections.Count)
        {
            // Swap the two neighbours' orders
            var a = sections[index];
            var b = sections[target];
            var tmp = a.SortOrder;
            a.MoveTo(b.SortOrder);
            b.MoveTo(tmp);
            await _context.SaveChangesAsync(cancellationToken);
        }

        return await GetSectionsAsync(cancellationToken);
    }

    public async Task<(byte[] Content, string FileName)> PreviewPdfAsync(
        string template, CancellationToken cancellationToken = default)
    {
        if (template is not ("dental" or "general"))
            throw new BadRequestException("Unknown template. Available: dental, general.");

        var tenant = await _context.Tenants
            .AsNoTracking()
            .FirstAsync(t => t.Id == _currentUser.TenantId, cancellationToken);

        var sections = await GetSectionsForTemplateAsync(template, cancellationToken);

        // Sample data so the admin sees EXACTLY what reception will print
        var sample = new PatientDto
        {
            FullName = "Muhammed Ashraf",
            PatientNumber = 123,
            FirstName = "Muhammed",
            LastName = "Ashraf",
            Phone = "+91 98765 43210",
            Email = "muhammed.ashraf@gmail.com",
            Address = "Main Road, Nadapuram, Kozhikode",
            Gender = "Male",
            Age = 34,
        };

        var pdf = IntakeFormPdfGenerator.Generate(
            tenant.Name, tenant.Address, tenant.Phone, sample, template, sections);
        return (pdf, $"intake-{template}-preview.pdf");
    }

    /// <summary>Sections that apply to a template ("both" applies everywhere).</summary>
    public async Task<List<IntakeFormSectionDto>> GetSectionsForTemplateAsync(
        string template, CancellationToken cancellationToken = default)
    {
        var sections = await _context.IntakeFormSections
            .AsNoTracking()
            .Where(s => s.Template == template || s.Template == "both")
            .OrderBy(s => s.SortOrder)
            .ToListAsync(cancellationToken);
        return sections.Select(MapToDto).ToList();
    }

    public async Task<IntakeFormResponseDto> SaveResponseAsync(
        Guid patientId, SaveIntakeFormResponseRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.Template is not ("dental" or "general"))
            throw new BadRequestException("Unknown template. Available: dental, general.");

        var patientExists = await _context.Patients
            .AnyAsync(p => p.Id == patientId, cancellationToken);
        if (!patientExists) throw new NotFoundException("Patient not found.");

        var json = JsonSerializer.Serialize(request.Answers ?? new IntakeAnswersDto());
        if (json.Length > 11_000)
            throw new BadRequestException("The answers are too long — shorten the text fields.");

        var response = new IntakeFormResponse(
            _currentUser.TenantId, patientId, request.Template, json, _currentUser.TenantUserId);
        _context.IntakeFormResponses.Add(response);
        await _context.SaveChangesAsync(cancellationToken);

        return (await GetLatestResponseAsync(patientId, cancellationToken))!;
    }

    public async Task<IntakeFormResponseDto?> GetLatestResponseAsync(
        Guid patientId, CancellationToken cancellationToken = default)
    {
        var latest = await _context.IntakeFormResponses
            .AsNoTracking()
            .Where(r => r.PatientId == patientId)
            .OrderByDescending(r => r.CreatedAt)
            .Select(r => new
            {
                r.Template,
                r.AnswersJson,
                r.CreatedAt,
                FilledBy = r.FilledBy.SystemUser.FirstName + " " + r.FilledBy.SystemUser.LastName
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (latest is null) return null;

        return new IntakeFormResponseDto
        {
            Template = latest.Template,
            Answers = JsonSerializer.Deserialize<IntakeAnswersDto>(latest.AnswersJson) ?? new(),
            FilledAt = latest.CreatedAt,
            FilledByName = latest.FilledBy,
        };
    }

    private async Task<IntakeFormSection> GetSectionAsync(Guid id, CancellationToken ct)
        => await _context.IntakeFormSections.FirstOrDefaultAsync(s => s.Id == id, ct)
           ?? throw new NotFoundException("Section not found.");

    private static (string Template, string Kind, string Title, List<string> Items) Validate(
        SaveIntakeFormSectionRequest request)
    {
        var template = request.Template?.Trim().ToLowerInvariant() ?? "";
        if (!Templates.Contains(template))
            throw new BadRequestException($"Template must be one of: {string.Join(", ", Templates)}.");

        var kind = request.Kind?.Trim().ToLowerInvariant() ?? "";
        if (!Kinds.Contains(kind))
            throw new BadRequestException($"Kind must be one of: {string.Join(", ", Kinds)}.");

        var title = request.Title?.Trim();
        if (string.IsNullOrWhiteSpace(title))
            throw new BadRequestException("Section title is required.");
        if (title.Length > 120)
            throw new BadRequestException("Section title is too long (max 120 characters).");

        var items = (request.Items ?? [])
            .Select(i => i?.Trim() ?? "")
            .Where(i => i.Length > 0)
            .ToList();

        if (kind is "lines" or "checklist")
        {
            if (items.Count == 0)
                throw new BadRequestException("Add at least one item for this section type.");
            if (items.Count > 14)
                throw new BadRequestException("Maximum 14 items per section.");
            if (items.Any(i => i.Length > 80))
                throw new BadRequestException("Items must be at most 80 characters.");
        }
        else
        {
            items = [];   // boxes have no items
        }

        return (template, kind, title!, items);
    }

    private static IntakeFormSectionDto MapToDto(IntakeFormSection section) => new()
    {
        Id = section.Id,
        Template = section.Template,
        Kind = section.Kind,
        Title = section.Title,
        Items = JsonSerializer.Deserialize<List<string>>(section.ItemsJson) ?? [],
        SortOrder = section.SortOrder,
    };
}
