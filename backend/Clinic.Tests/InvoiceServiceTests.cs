using Clinic.Application.Common.Exceptions;
using Clinic.Application.Common.Models;
using Clinic.Application.Features.Invoices.DTOs;
using Clinic.Infrastructure.Services;
using Clinic.Tests.TestInfrastructure;

namespace Clinic.Tests;

/// <summary>
/// Patient billing: totals are server-computed, numbering is per-clinic,
/// paid/cancel transitions are guarded, stats sum only PAID money, and
/// invoices never leak across clinics.
/// </summary>
public class InvoiceServiceTests : IDisposable
{
    private readonly TestDb _db = new();

    private InvoiceService CreateService() => new(_db.CreateContext(), _db.CurrentUser);

    private async Task<Guid> SeedPatientAsync(string clinic = "Smile Dental", string email = "admin@clinic.com")
    {
        var tenant = await _db.SeedTenantAsync(clinic, email);
        _db.CurrentUser.ActAs(tenant.TenantId, tenant.TenantUserId);

        var patients = new PatientService(_db.CreateContext(), _db.CurrentUser);
        var patient = await patients.RegisterPatientAsync(
            new Clinic.Application.Features.Patients.DTOs.RegisterPatientRequest
            {
                FirstName = "Fathima", LastName = "Rasheed",
                Phone = "+91 90000 22222", Gender = "Female",
            });
        return patient.Id;
    }

    private static CreateInvoiceRequest RootCanalBill(Guid patientId, bool markPaid = false) => new()
    {
        PatientId = patientId,
        Items =
        [
            new CreateInvoiceItem { Description = "Root canal — first sitting", Quantity = 1, UnitPriceRupees = 3500 },
            new CreateInvoiceItem { Description = "X-ray (IOPA)", Quantity = 2, UnitPriceRupees = 250 },
        ],
        DiscountRupees = 500,
        MarkPaid = markPaid,
        PaymentMethod = markPaid ? "Upi" : null,
    };

    [Fact]
    public async Task Create_ComputesTotals_AndNumbersPerClinic()
    {
        var patientId = await SeedPatientAsync();

        var first = await CreateService().CreateAsync(RootCanalBill(patientId));
        var second = await CreateService().CreateAsync(RootCanalBill(patientId));

        Assert.Equal(1, first.InvoiceNumber);
        Assert.Equal(2, second.InvoiceNumber);
        Assert.Equal(4000, first.SubtotalRupees);   // 3500 + 2×250
        Assert.Equal(3500, first.TotalRupees);      // − 500 discount
        Assert.Equal("Unpaid", first.Status);
        Assert.Equal("Fathima Rasheed", first.PatientName);
    }

    [Fact]
    public async Task MarkPaid_Flows_AndCancelGuards()
    {
        var patientId = await SeedPatientAsync();
        var invoice = await CreateService().CreateAsync(RootCanalBill(patientId));

        var paid = await CreateService().MarkPaidAsync(
            invoice.Id, new MarkInvoicePaidRequest { PaymentMethod = "Cash" });
        Assert.Equal("Paid", paid.Status);
        Assert.NotNull(paid.PaidAt);

        // Paid invoices can't be cancelled — corrections, not deletions
        await Assert.ThrowsAsync<BadRequestException>(
            () => CreateService().CancelAsync(invoice.Id));

        // Unknown method rejected
        var another = await CreateService().CreateAsync(RootCanalBill(patientId));
        await Assert.ThrowsAsync<BadRequestException>(
            () => CreateService().MarkPaidAsync(another.Id,
                new MarkInvoicePaidRequest { PaymentMethod = "Gold" }));
    }

    [Fact]
    public async Task Garbage_IsRejected()
    {
        var patientId = await SeedPatientAsync();
        var service = CreateService();

        await Assert.ThrowsAsync<BadRequestException>(() => service.CreateAsync(
            new CreateInvoiceRequest { PatientId = patientId, Items = [] }));
        await Assert.ThrowsAsync<BadRequestException>(() => service.CreateAsync(
            new CreateInvoiceRequest
            {
                PatientId = patientId,
                Items = [new CreateInvoiceItem { Description = "X", Quantity = 0, UnitPriceRupees = 10 }],
            }));
        // Discount larger than the bill
        await Assert.ThrowsAsync<BadRequestException>(() => service.CreateAsync(
            new CreateInvoiceRequest
            {
                PatientId = patientId,
                Items = [new CreateInvoiceItem { Description = "Consult", Quantity = 1, UnitPriceRupees = 100 }],
                DiscountRupees = 500,
            }));
    }

    [Fact]
    public async Task Stats_CountOnlyPaidMoney()
    {
        var patientId = await SeedPatientAsync();
        await CreateService().CreateAsync(RootCanalBill(patientId, markPaid: true));   // 3500 paid
        await CreateService().CreateAsync(RootCanalBill(patientId));                    // 3500 unpaid

        var stats = await CreateService().GetStatsAsync();

        Assert.Equal(3500, stats.TodayCollectedRupees);
        Assert.Equal(3500, stats.MonthCollectedRupees);
        Assert.Equal(1, stats.UnpaidCount);
        Assert.Equal(3500, stats.UnpaidTotalRupees);
    }

    [Fact]
    public async Task Invoices_AreInvisibleToOtherClinics_AndPdfRenders()
    {
        var patientId = await SeedPatientAsync("Clinic A", "a@clinic.com");
        var invoice = await CreateService().CreateAsync(RootCanalBill(patientId, markPaid: true));

        var (pdf, fileName) = await CreateService().GetPdfAsync(invoice.Id);
        Assert.StartsWith("%PDF", System.Text.Encoding.ASCII.GetString(pdf, 0, 4));
        Assert.Equal("invoice-INV000001.pdf", fileName);

        _db.CurrentUser.ActAsAnonymous();
        var clinicB = await _db.SeedTenantAsync("Clinic B", "b@clinic.com");
        _db.CurrentUser.ActAs(clinicB.TenantId, clinicB.TenantUserId);

        var visible = await CreateService().GetAllAsync(null, null, new PageRequest());
        Assert.Empty(visible.Items);
        await Assert.ThrowsAsync<NotFoundException>(() => CreateService().GetByIdAsync(invoice.Id));
    }

    public void Dispose() => _db.Dispose();
}
