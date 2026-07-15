namespace Clinic.Application.Common.Interfaces;

/// <summary>
/// Outbound email. Implementations must NEVER throw into business flows —
/// a failed email must not fail a registration. Returns whether the message
/// was actually handed to the mail server (false = unconfigured or failed),
/// so diagnostics like the platform test-email can report honestly.
/// </summary>
public interface IEmailSender
{
    Task<bool> SendAsync(string to, string subject, string htmlBody, CancellationToken cancellationToken = default);
}
