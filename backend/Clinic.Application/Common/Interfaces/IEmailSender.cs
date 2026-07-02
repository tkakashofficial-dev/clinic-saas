namespace Clinic.Application.Common.Interfaces;

/// <summary>
/// Outbound email. Implementations must NEVER throw into business flows —
/// a failed email must not fail a registration.
/// </summary>
public interface IEmailSender
{
    Task SendAsync(string to, string subject, string htmlBody, CancellationToken cancellationToken = default);
}
