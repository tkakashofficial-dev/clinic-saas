using Clinic.Application.Common.Interfaces;

namespace Clinic.Tests.TestInfrastructure;

/// <summary>Email double: records what would have been sent.</summary>
public class NoOpEmailSender : IEmailSender
{
    public List<(string To, string Subject, string Body)> Sent { get; } = new();

    public Task<bool> SendAsync(string to, string subject, string htmlBody, CancellationToken cancellationToken = default)
    {
        Sent.Add((to, subject, htmlBody));
        return Task.FromResult(true);
    }
}
