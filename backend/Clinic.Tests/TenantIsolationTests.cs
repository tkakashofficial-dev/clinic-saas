using Clinic.Domain.Entities;
using Clinic.Domain.Enums;
using Clinic.Tests.TestInfrastructure;
using Microsoft.EntityFrameworkCore;

namespace Clinic.Tests;

/// <summary>
/// The most important tests in the suite: if any of these fail, one clinic
/// can see or modify another clinic's patient data.
/// </summary>
public class TenantIsolationTests : IDisposable
{
    private readonly TestDb _db = new();

    [Fact]
    public async Task QueryFilter_ReturnsOnlyCurrentTenantsPatients()
    {
        var clinicA = await _db.SeedTenantAsync("Clinic A", "a@a.com");
        var clinicB = await _db.SeedTenantAsync("Clinic B", "b@b.com");

        await AddPatientAsync(clinicA.TenantId, clinicA.TenantUserId, "Alice", "+3111111111");
        await AddPatientAsync(clinicB.TenantId, clinicB.TenantUserId, "Bob", "+3122222222");

        // Act as clinic A — the global query filter must hide clinic B's data
        _db.CurrentUser.ActAs(clinicA.TenantId, clinicA.TenantUserId);
        await using var context = _db.CreateContext();
        var visible = await context.Patients.ToListAsync();

        Assert.Single(visible);
        Assert.Equal("Alice", visible[0].FirstName);
    }

    [Fact]
    public async Task CrossTenantWrite_IsBlocked()
    {
        var clinicA = await _db.SeedTenantAsync("Clinic A", "a@a.com");
        var clinicB = await _db.SeedTenantAsync("Clinic B", "b@b.com");

        // Act as clinic A but try to write a patient INTO clinic B
        _db.CurrentUser.ActAs(clinicA.TenantId, clinicA.TenantUserId);
        await using var context = _db.CreateContext();
        context.Patients.Add(new Patient(
            clinicB.TenantId, clinicB.TenantUserId,
            "Sneaky", "Write", "+3133333333", Gender.Other, null));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => context.SaveChangesAsync());
        Assert.Contains("Cross-tenant write blocked", ex.Message);
    }

    [Fact]
    public async Task AddedEntity_WithEmptyTenantId_IsStampedWithCurrentTenant()
    {
        var clinicA = await _db.SeedTenantAsync("Clinic A", "a@a.com");

        _db.CurrentUser.ActAs(clinicA.TenantId, clinicA.TenantUserId);
        await using var context = _db.CreateContext();
        var patient = new Patient(
            Guid.Empty, clinicA.TenantUserId,   // "forgot" the tenant id
            "Auto", "Stamped", "+3144444444", Gender.Female, null);
        context.Patients.Add(patient);
        await context.SaveChangesAsync();

        Assert.Equal(clinicA.TenantId, patient.TenantId);
    }

    [Fact]
    public async Task UnauthenticatedWrite_WithoutTenantId_Throws()
    {
        _db.CurrentUser.ActAsAnonymous();
        await using var context = _db.CreateContext();
        context.Patients.Add(new Patient(
            Guid.Empty, Guid.NewGuid(),
            "No", "Tenant", "+3155555555", Gender.Male, null));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => context.SaveChangesAsync());
    }

    private async Task AddPatientAsync(
        Guid tenantId, Guid tenantUserId, string firstName, string phone)
    {
        // Seed while acting as that tenant so writes pass the tenant guard
        _db.CurrentUser.ActAs(tenantId, tenantUserId);
        await using var context = _db.CreateContext();
        context.Patients.Add(new Patient(
            tenantId, tenantUserId, firstName, "Test", phone, Gender.Other, null));
        await context.SaveChangesAsync();
    }

    public void Dispose() => _db.Dispose();
}
