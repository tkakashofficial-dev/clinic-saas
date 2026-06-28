using Clinic.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Clinic.Infrastructure.Persistence;

public class ClinicDbContextFactory : IDesignTimeDbContextFactory<ClinicDbContext>
{
    public ClinicDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<ClinicDbContext>();

        optionsBuilder.UseNpgsql(
            "Host=localhost;Port=5432;Database=clinic_db;Username=postgres;Password=admin@789");

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