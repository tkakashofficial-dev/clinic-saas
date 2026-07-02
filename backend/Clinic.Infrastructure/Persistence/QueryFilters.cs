namespace Clinic.Infrastructure.Persistence;

/// <summary>
/// Names of the global query filters applied in <see cref="ClinicDbContext"/>.
/// Use with IgnoreQueryFilters([...]) to opt out of ONE filter without losing the others.
/// </summary>
public static class QueryFilters
{
    public const string SoftDelete = "SoftDelete";
    public const string Tenant = "Tenant";
}
