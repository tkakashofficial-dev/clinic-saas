using Clinic.Application.Features.Auth.DTOs;
using Clinic.Application.Features.Auth.Services;
using Microsoft.AspNetCore.Mvc;

namespace Clinic.Api.Controllers;

// No try/catch needed anywhere — GlobalExceptionHandler translates
// application exceptions into RFC 7807 Problem Details responses.
[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;

    public AuthController(IAuthService authService)
    {
        _authService = authService;
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
}
