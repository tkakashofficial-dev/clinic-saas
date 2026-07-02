using Clinic.Domain.Constants;
using Clinic.Domain.Entities;
using Clinic.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Clinic.Tests.TestInfrastructure;

/// <summary>
/// SQLite in-memory database for tests: a real relational engine (enforces
/// foreign keys and unique indexes, runs the global query filters) without
/// needing PostgreSQL installed — which keeps tests runnable anywhere, incl. CI.
/// The connection must stay open for the lifetime of the database.
/// </summary>
public sealed class TestDb : IDisposable
{
    private readonly SqliteConnection _connection;

    public FakeCurrentUserService CurrentUser { get; } = new();

    public TestDb()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        using var context = CreateContext();
        context.Database.EnsureCreated();
    }

    public ClinicDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<ClinicDbContext>()
                .UseSqlite(_connection)
                .Options,
            CurrentUser);

    /// <summary>
    /// Seeds a complete tenant graph (tenant + admin user + the three roles)
    /// and returns the ids needed to act as that tenant's admin.
    /// </summary>
    public async Task<(Guid TenantId, Guid TenantUserId, Guid SystemUserId)> SeedTenantAsync(
        string clinicName,
        string adminEmail)
    {
        await using var context = CreateContext();

        var tenant = new Tenant(clinicName);
        context.Tenants.Add(tenant);

        var systemUser = new SystemUser(
            adminEmail,
            BCrypt.Net.BCrypt.HashPassword("Test-Pass-123"),
            "Test",
            "Admin");
        context.SystemUsers.Add(systemUser);

        var tenantUser = new TenantUser(tenant.Id, systemUser.Id);
        context.TenantUsers.Add(tenantUser);

        Role adminRole = default!;
        foreach (var roleName in RoleNames.All)
        {
            var role = new Role(tenant.Id, roleName);
            context.Roles.Add(role);
            if (roleName == RoleNames.Admin)
                adminRole = role;
        }

        context.TenantUserRoles.Add(new TenantUserRole(tenantUser.Id, adminRole.Id));

        await context.SaveChangesAsync();
        return (tenant.Id, tenantUser.Id, systemUser.Id);
    }

    public void Dispose() => _connection.Dispose();
}
