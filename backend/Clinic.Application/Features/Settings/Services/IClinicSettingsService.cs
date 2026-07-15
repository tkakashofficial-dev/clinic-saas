using Clinic.Application.Features.Settings.DTOs;

namespace Clinic.Application.Features.Settings.Services;

public interface IClinicSettingsService
{
    /// <summary>Readable by every role — the patients screen needs the
    /// default template; PDFs need name/phone/address.</summary>
    Task<ClinicSettingsDto> GetAsync(CancellationToken cancellationToken = default);

    /// <summary>Admin only (enforced at the controller).</summary>
    Task<ClinicSettingsDto> UpdateAsync(
        UpdateClinicSettingsRequest request, CancellationToken cancellationToken = default);
}
