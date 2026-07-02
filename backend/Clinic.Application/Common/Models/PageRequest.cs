namespace Clinic.Application.Common.Models;

/// <summary>
/// Pagination input. Values are normalized server-side — clients cannot request
/// unbounded pages (a classic way to take down an API).
/// </summary>
public record PageRequest(int Page = 1, int PageSize = PageRequest.DefaultPageSize)
{
    public const int DefaultPageSize = 20;
    public const int MaxPageSize = 100;

    public int NormalizedPage => Math.Max(1, Page);
    public int NormalizedPageSize => Math.Clamp(PageSize, 1, MaxPageSize);
}
