namespace Clinic.Application.Features.Patients.DTOs;

/// <summary>Outcome of a CSV patient import — honest accounting so the
/// clinic knows exactly which rows made it in and which need fixing.</summary>
public class ImportResultDto
{
    public int Imported { get; set; }
    public int Skipped { get; set; }
    public List<ImportRowError> Errors { get; set; } = new();
}

public class ImportRowError
{
    /// <summary>1-based row number in the file (header is row 1).</summary>
    public int Row { get; set; }
    public string Message { get; set; } = default!;
}
