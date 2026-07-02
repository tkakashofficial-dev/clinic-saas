using Clinic.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Clinic.Infrastructure.Persistence;

public class ClinicDbContextFactory : IDesignTimeDbContextFactory<ClinicDbContext>
{
    public ClinicDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<ClinicDbContext>();

        // Used only by `dotnet ef` at design time. Never hardcode credentials here —
        // set CLINIC_DB_CONNECTION when a real connection is needed (e.g. `database update`).
        var connectionString =
            Environment.GetEnvironmentVariable("CLINIC_DB_CONNECTION")
            ?? "Host=localhost;Port=5432;Database=clinic_db;Username=postgres";

        optionsBuilder.UseNpgsql(connectionString);

        return new ClinicDbContext(
            optionsBuilder.Options,
            new DesignTimeCurrentUserService());
    }
}

public class DesignTimeCurrentUserService : ICurrentUserService
{
    public Guid UserId => Guid.Empty;
    public Guid TenantId => Guid.Empty;
    public Guid TenantUserId => Guid.Empty;  // ADD THIS
    public string? Email => null;
    public IEnumerable<string> Roles => new List<string>();
    public bool IsAuthenticated => false;
}