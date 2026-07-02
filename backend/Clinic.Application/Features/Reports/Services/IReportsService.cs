using Clinic.Application.Features.Reports.DTOs;

namespace Clinic.Application.Features.Reports.Services;

public interface IReportsService
{
    Task<PracticeOverviewDto> GetPracticeOverviewAsync(CancellationToken cancellationToken = default);

    /// <summary>Same overview rendered as a branded, printable PDF.</summary>
    Task<(byte[] Content, string FileName)> GetPracticeOverviewPdfAsync(CancellationToken cancellationToken = default);
}
