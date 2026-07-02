using Clinic.Application.Features.Auth.DTOs;

namespace Clinic.Application.Features.Auth.Services;

public interface IAuthService
{
    Task<AuthResponse> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken = default);
    Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default);

    /// <summary>Exchanges a valid refresh token for a new token pair (rotation: the old one is revoked).</summary>
    Task<AuthResponse> RefreshAsync(RefreshRequest request, CancellationToken cancellationToken = default);

    /// <summary>An existing user opens an additional clinic and becomes its Admin.</summary>
    Task<AuthResponse> CreateClinicAsync(Guid systemUserId, CreateClinicRequest request, CancellationToken cancellationToken = default);

    /// <summary>Issues a token pair scoped to another clinic the user belongs to.</summary>
    Task<AuthResponse> SwitchClinicAsync(Guid systemUserId, SwitchClinicRequest request, CancellationToken cancellationToken = default);
}