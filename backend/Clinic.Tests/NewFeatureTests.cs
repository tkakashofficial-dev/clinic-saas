using Clinic.Application.Common.Exceptions;
using Clinic.Application.Features.Invoices.DTOs;
using Clinic.Application.Features.Inventory.DTOs;
using Clinic.Application.Features.Patients.DTOs;
using Clinic.Application.Features.PublicBooking.DTOs;
using Clinic.Domain.Constants;
using Clinic.Domain.Entities;
using Clinic.Infrastructure.Services;
using Clinic.Tests.TestInfrastructure;
using Microsoft.EntityFrameworkCore;

namespace Clinic.Tests;

/// <summary>
/// Covers the 2026-07 feature batch: blood group + condition sync, CSV
/// import/export, the dues report, low-stock alerts and public booking.
/// </summary>
public class NewFeatureTests : IDisposable
{
    private readonly TestDb _db = new();

    private PatientService PatientService() => new(_db.CreateContext(), _db.CurrentUser);
    private InvoiceService InvoiceService() => new(_db.CreateContext(), _db.CurrentUser);
    private InventoryService InventoryService() => new(_db.CreateContext(), _db.CurrentUser);
    private PublicBookingService PublicBookingService() => new(_db.CreateContext());

    // ---------- blood group + condition sync ----------

    [Fact]
    public async Task RegisterAndUpdate_SyncsConditionsAndBloodGroup()
    {
        var clinic = await _db.SeedTenantAsync("Clinic", "a@a.com");
        _db.CurrentUser.ActAs(clinic.TenantId, clinic.TenantUserId);

        var registered = await PatientService().RegisterPatientAsync(new RegisterPatientRequest
        {
            FirstName = "Asha",
            LastName = "Nair",
            Phone = "+919876543210",
            Gender = "Female",
            BloodGroup = "o+",                       // lowercase on purpose
            MedicalConditionCodes = ["DIABETES"],
        });

        Assert.Equal("O+", registered.BloodGroup);   // normalized
        Assert.Contains("DIABETES", registered.MedicalConditionCodes);

        // Edit replaces the set: diabetes unchecked, two others checked
        var updated = await PatientService().UpdatePatientAsync(registered.Id,
            new UpdatePatientRequest
            {
                FirstName = "Asha",
                LastName = "Nair",
                Phone = "+919876543210",
                Gender = "Female",
                BloodGroup = "AB-",
                MedicalConditionCodes = ["PREGNANCY", "CARDIAC"],
            });

        Assert.Equal("AB-", updated.BloodGroup);
        Assert.Equal(2, updated.MedicalConditionCodes.Count);
        Assert.DoesNotContain("DIABETES", updated.MedicalConditionCodes);
        Assert.Contains("PREGNANCY", updated.MedicalConditionCodes);
    }

    // ---------- CSV import / export ----------

    [Fact]
    public async Task ImportCsv_ImportsValidRows_SkipsBadOnes_WithRowErrors()
    {
        var clinic = await _db.SeedTenantAsync("Clinic", "a@a.com");
        _db.CurrentUser.ActAs(clinic.TenantId, clinic.TenantUserId);

        // Header uses spaced names + shuffled column order on purpose;
        // row 3 has no name, row 5 repeats row 2's phone
        const string csv = """
            Phone,First Name,Last Name,Date Of Birth,Blood Group,Gender
            9876543210,Asha,Nair,15-03-1985,o+,female
            9812345678,Biju,Menon,,,male
            123,NoPhone,Row,,,
            9876543210,Dup,Licate,,,
            """;

        var result = await PatientService().ImportCsvAsync(csv);

        Assert.Equal(2, result.Imported);
        Assert.Equal(2, result.Skipped);
        Assert.Equal(2, result.Errors.Count);

        var asha = await PatientService().GetAllPatientsAsync("Asha",
            new Application.Common.Models.PageRequest(1, 10));
        var patient = Assert.Single(asha.Items);
        Assert.Equal("O+", patient.BloodGroup);
        Assert.Equal(new DateOnly(1985, 3, 15), patient.DateOfBirth);
    }

    [Fact]
    public async Task ExportCsv_ContainsRegisteredPatient()
    {
        var clinic = await _db.SeedTenantAsync("Clinic", "a@a.com");
        _db.CurrentUser.ActAs(clinic.TenantId, clinic.TenantUserId);

        await PatientService().RegisterPatientAsync(new RegisterPatientRequest
        {
            FirstName = "Comma, Name",   // must survive CSV escaping
            LastName = "Test",
            Phone = "+919000000001",
            Gender = "Other",
        });

        var csv = await PatientService().ExportCsvAsync();

        Assert.StartsWith("PatientNumber,FirstName", csv);
        Assert.Contains("\"Comma, Name\"", csv);
        Assert.Contains("P-000001", csv);
    }

    // ---------- dues report ----------

    [Fact]
    public async Task GetDues_GroupsUnpaidPerPatient_IgnoresPaid()
    {
        var clinic = await _db.SeedTenantAsync("Clinic", "a@a.com");
        _db.CurrentUser.ActAs(clinic.TenantId, clinic.TenantUserId);

        var patient = await PatientService().RegisterPatientAsync(new RegisterPatientRequest
        {
            FirstName = "Debt", LastName = "Or", Phone = "+919000000002", Gender = "Male",
        });

        CreateInvoiceRequest Invoice(decimal amount, bool paid) => new()
        {
            PatientId = patient.Id,
            Items = [new CreateInvoiceItem { Description = "Filling", Quantity = 1, UnitPriceRupees = amount }],
            MarkPaid = paid,
            PaymentMethod = paid ? "Upi" : null,
        };

        await InvoiceService().CreateAsync(Invoice(500, paid: false));
        await InvoiceService().CreateAsync(Invoice(700, paid: false));
        await InvoiceService().CreateAsync(Invoice(999, paid: true));

        var dues = await InvoiceService().GetDuesAsync();

        var row = Assert.Single(dues.Rows);
        Assert.Equal(2, row.UnpaidCount);
        Assert.Equal(1200, row.OutstandingRupees);
        Assert.Equal(1200, dues.TotalOutstandingRupees);
        Assert.Equal(1, dues.PatientsWithDues);
    }

    // ---------- low-stock alert ----------

    [Fact]
    public async Task AdjustStock_CrossingReorderLevel_NotifiesAdmin_Once()
    {
        var clinic = await _db.SeedTenantAsync("Clinic", "a@a.com");
        _db.CurrentUser.ActAs(clinic.TenantId, clinic.TenantUserId);

        var item = await InventoryService().CreateAsync(new SaveInventoryItemRequest
        {
            Name = "Lidocaine", Category = "Medicine", Unit = "vial",
            StockQuantity = 10, ReorderLevel = 3,
        });

        // 10 → 2 crosses the level → one alert for the (single) admin
        await InventoryService().AdjustStockAsync(item.Id, new AdjustStockRequest { Delta = -8 });
        // 2 → 1 stays below → no second alert
        await InventoryService().AdjustStockAsync(item.Id, new AdjustStockRequest { Delta = -1 });

        await using var context = _db.CreateContext();
        var alerts = await context.Notifications
            .Where(n => n.Type == NotificationTypes.Inventory)
            .ToListAsync();

        var alert = Assert.Single(alerts);
        Assert.Equal(clinic.TenantUserId, alert.RecipientTenantUserId);
        Assert.Contains("Lidocaine", alert.Message);
    }

    // ---------- public booking ----------

    private async Task<(Guid TenantId, Guid AdminTenantUserId)> SeedBookableClinicAsync(
        string slug)
    {
        var clinic = await _db.SeedTenantAsync("Smile Dental", $"{slug}@a.com");

        await using var context = _db.CreateContext();
        var tenant = await context.Tenants.FirstAsync(t => t.Id == clinic.TenantId);
        tenant.AssignSlug(slug);
        tenant.SetPublicBooking(true);

        // Give the admin the Doctor role too, so the clinic has a bookable doctor
        var doctorRole = await context.Roles
            .IgnoreQueryFilters()
            .FirstAsync(r => r.TenantId == clinic.TenantId && r.Name == RoleNames.Doctor);
        context.TenantUserRoles.Add(new TenantUserRole(clinic.TenantUserId, doctorRole.Id));

        await context.SaveChangesAsync();
        return (clinic.TenantId, clinic.TenantUserId);
    }

    [Fact]
    public async Task PublicBooking_UnknownOrDisabledSlug_Throws404()
    {
        _db.CurrentUser.ActAsAnonymous();

        await Assert.ThrowsAsync<NotFoundException>(
            () => PublicBookingService().GetClinicAsync("nope"));
    }

    [Fact]
    public async Task PublicBooking_HappyPath_CreatesPatientAppointmentAndBells()
    {
        var (tenantId, adminId) = await SeedBookableClinicAsync("smile-dental");
        _db.CurrentUser.ActAsAnonymous();

        var clinicInfo = await PublicBookingService().GetClinicAsync("smile-dental");
        var doctor = Assert.Single(clinicInfo.Doctors);

        var result = await PublicBookingService().BookAsync("smile-dental",
            new PublicBookingRequest
            {
                DoctorId = doctor.Id,
                AppointmentAt = DateTime.UtcNow.AddDays(1),
                PatientName = "Walk In Wilson",
                Phone = "9855512345",
                Note = "Tooth pain",
            });

        Assert.Equal("Smile Dental", result.ClinicName);

        await using var context = _db.CreateContext();
        var patient = await context.Patients.IgnoreQueryFilters()
            .SingleAsync(p => p.TenantId == tenantId && p.Phone == "9855512345");
        Assert.Equal("Walk", patient.FirstName);
        Assert.Equal("In Wilson", patient.LastName);
        Assert.Equal(1, patient.PatientNumber);

        var appointment = await context.Appointments.IgnoreQueryFilters()
            .SingleAsync(a => a.TenantId == tenantId);
        Assert.StartsWith("[Booked online]", appointment.Notes);

        // The doctor-admin gets exactly one bell (recipients are de-duplicated)
        var bells = await context.Notifications.IgnoreQueryFilters()
            .Where(n => n.TenantId == tenantId && n.Type == NotificationTypes.Booking)
            .ToListAsync();
        Assert.Single(bells);
        Assert.Equal(adminId, bells[0].RecipientTenantUserId);
    }

    [Fact]
    public async Task PublicBooking_SecondBookingSameDay_Conflicts()
    {
        await SeedBookableClinicAsync("busy-clinic");
        _db.CurrentUser.ActAsAnonymous();

        var doctor = (await PublicBookingService().GetClinicAsync("busy-clinic")).Doctors[0];
        var at = DateTime.UtcNow.Date.AddDays(2).AddHours(10);

        PublicBookingRequest Request(DateTime when) => new()
        {
            DoctorId = doctor.Id,
            AppointmentAt = when,
            PatientName = "Eager Beaver",
            Phone = "9855598765",
        };

        await PublicBookingService().BookAsync("busy-clinic", Request(at));

        await Assert.ThrowsAsync<ConflictException>(
            () => PublicBookingService().BookAsync("busy-clinic", Request(at.AddHours(2))));
    }

    [Fact]
    public async Task PublicBooking_Honeypot_WritesNothing()
    {
        var (tenantId, _) = await SeedBookableClinicAsync("bot-target");
        _db.CurrentUser.ActAsAnonymous();

        var doctor = (await PublicBookingService().GetClinicAsync("bot-target")).Doctors[0];

        var result = await PublicBookingService().BookAsync("bot-target",
            new PublicBookingRequest
            {
                DoctorId = doctor.Id,
                AppointmentAt = DateTime.UtcNow.AddDays(1),
                PatientName = "Bot Bot",
                Phone = "9800000000",
                Website = "https://spam.example",   // the honeypot
            });

        Assert.Equal("Smile Dental", result.ClinicName);   // pretends success

        await using var context = _db.CreateContext();
        Assert.False(await context.Appointments.IgnoreQueryFilters()
            .AnyAsync(a => a.TenantId == tenantId));
        Assert.False(await context.Patients.IgnoreQueryFilters()
            .AnyAsync(p => p.TenantId == tenantId));
    }

    public void Dispose() => _db.Dispose();
}
