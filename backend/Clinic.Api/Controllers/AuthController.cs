using Clinic.Application.Common.Interfaces;
using Clinic.Application.Features.Auth.DTOs;
using Clinic.Application.Features.Auth.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Clinic.Api.Controllers;

// No try/catch needed anywhere — GlobalExceptionHandler translates
// application exceptions into RFC 7807 Problem Details responses.
// Rate limited: auth endpoints are the brute-force target.
[ApiController]
[Route("api/[controller]")]
[EnableRateLimiting("auth")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;
    private readonly ICurrentUserService _currentUser;

    public AuthController(IAuthService authService, ICurrentUserService currentUser)
    {
        _authService = authService;
        _currentUser = currentUser;
    }

    [HttpPost("register")]
    public async Task<ActionResult<AuthResponse>> Register(
        [FromBody] RegisterRequest request,
        CancellationToken cancellationToken)
        => Ok(await _authService.RegisterAsync(request, cancellationToken));

    [HttpPost("login")]
    public async Task<ActionResult<AuthResponse>> Login(
        [FromBody] LoginRequest request,
        CancellationToken cancellationToken)
        => Ok(await _authService.LoginAsync(request, cancellationToken));

    [HttpPost("refresh")]
    public async Task<ActionResult<AuthResponse>> Refresh(
        [FromBody] RefreshRequest request,
        CancellationToken cancellationToken)
        => Ok(await _authService.RefreshAsync(request, cancellationToken));

    /// <summary>Multi-clinic owners: get a token scoped to another of their clinics.</summary>
    [HttpPost("switch-clinic")]
    [Authorize]
    public async Task<ActionResult<AuthResponse>> SwitchClinic(
        [FromBody] SwitchClinicRequest request,
        CancellationToken cancellationToken)
        => Ok(await _authService.SwitchClinicAsync(_currentUser.UserId, request, cancellationToken));

    /// <summary>Open an additional clinic — the caller becomes its Admin.</summary>
    [HttpPost("clinics")]
    [Authorize]
    public async Task<ActionResult<AuthResponse>> CreateClinic(
        [FromBody] CreateClinicRequest request,
        CancellationToken cancellationToken)
        => Ok(await _authService.CreateClinicAsync(_currentUser.UserId, request, cancellationToken));
}
