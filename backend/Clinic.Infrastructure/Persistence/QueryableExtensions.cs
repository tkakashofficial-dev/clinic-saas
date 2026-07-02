using Clinic.Application.Common.Models;
using Microsoft.EntityFrameworkCore;

namespace Clinic.Infrastructure.Persistence;

public static class QueryableExtensions
{
    /// <summary>
    /// Executes a paged query: one COUNT + one page SELECT.
    /// IMPORTANT: apply all filters BEFORE calling this, and ensure the query
    /// has a stable OrderBy — Skip/Take without ordering returns rows in
    /// unpredictable order in PostgreSQL.
    /// </summary>
    public static async Task<PagedResult<T>> ToPagedResultAsync<T>(
        this IQueryable<T> query,
        PageRequest page,
        CancellationToken cancellationToken = default)
    {
        var pageNumber = page.NormalizedPage;
        var pageSize = page.NormalizedPageSize;

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<T>
        {
            Items = items,
            Page = pageNumber,
            PageSize = pageSize,
            TotalCount = totalCount
        };
    }
}
