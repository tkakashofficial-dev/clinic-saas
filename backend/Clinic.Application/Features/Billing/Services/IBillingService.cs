using Clinic.Application.Features.Billing.DTOs;

namespace Clinic.Application.Features.Billing.Services;

public interface IBillingService
{
    Task<BillingSummaryDto> GetSummaryAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Switches the clinic's plan. Beta: instant, no payment.
    /// Later: this is where the payment-gateway (Razorpay) handshake goes.
    /// </summary>
    Task<BillingSummaryDto> ChangePlanAsync(ChangePlanRequest request, CancellationToken cancellationToken = default);
}
