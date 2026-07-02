namespace Clinic.Application.Features.Auth.DTOs;

public class ForgotPasswordRequest
{
    public string Email { get; set; } = default!;
}

public class ResetPasswordRequest
{
    public string Token { get; set; } = default!;
    public string NewPassword { get; set; } = default!;
}
