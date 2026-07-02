using Clinic.Application.Features.Reports.DTOs;

namespace Clinic.Application.Features.Reports.Services;

public interface IReportsService
{
    Task<PracticeOverviewDto> GetPracticeOverviewAsync(CancellationToken cancellationToken = default);
}
