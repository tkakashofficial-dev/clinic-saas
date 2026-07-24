using Clinic.Application.Features.PublicBooking.DTOs;

namespace Clinic.Application.Features.PublicBooking.Services;

/// <summary>
/// The anonymous booking flow behind /book/{slug}. Runs WITHOUT a tenant
/// token, so every query pins the tenant explicitly — nothing here may
/// trust ICurrentUserService.
/// </summary>
public interface IPublicBookingService
{
    /// <summary>Clinic profile + bookable doctors, or 404 if the slug is
    /// unknown / booking is switched off.</summary>
    Task<PublicClinicDto> GetClinicAsync(
        string slug, CancellationToken cancellationToken = default);

    /// <summary>Books the visit: finds-or-registers the patient by phone,
    /// creates a Scheduled appointment, rings the clinic's bells.</summary>
    Task<PublicBookingResultDto> BookAsync(
        string slug, PublicBookingRequest request,
        CancellationToken cancellationToken = default);
}
