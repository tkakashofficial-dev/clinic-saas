using Clinic.Application.Common.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Net;
using System.Net.Mail;

namespace Clinic.Infrastructure.Services;

public class EmailSettings
{
    public string Host { get; set; } = "smtp.gmail.com";
    public int Port { get; set; } = 587;
    public string User { get; set; } = "";
    public string Password { get; set; } = "";       // Gmail App Password — user-secrets/env, never git
    public string FromName { get; set; } = "Klivia";

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(User) && !string.IsNullOrWhiteSpace(Password);
}

/// <summary>
/// SMTP sender (works with free Gmail App Passwords, ~500 mails/day —
/// plenty for beta; swap host/credentials for a transactional provider later).
/// Silently no-ops when not configured so local dev needs no email setup.
/// </summary>
public class SmtpEmailSender : IEmailSender
{
    private readonly EmailSettings _settings;
    private readonly ILogger<SmtpEmailSender> _logger;

    public SmtpEmailSender(IOptions<EmailSettings> settings, ILogger<SmtpEmailSender> logger)
    {
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task<bool> SendAsync(
        string to, string subject, string htmlBody, CancellationToken cancellationToken = default)
    {
        if (!_settings.IsConfigured)
        {
            _logger.LogInformation("Email not configured — skipping '{Subject}' to {To}", subject, to);
            return false;
        }

        try
        {
            using var client = new SmtpClient(_settings.Host, _settings.Port)
            {
                EnableSsl = true,
                Credentials = new NetworkCredential(_settings.User, _settings.Password),
                // Fail fast: if the mail host is unreachable (e.g. a PaaS that
                // blocks outbound SMTP), don't hang for the default 100 seconds
                Timeout = 20_000
            };

            using var message = new MailMessage
            {
                From = new MailAddress(_settings.User, _settings.FromName),
                Subject = subject,
                Body = htmlBody,
                IsBodyHtml = true
            };
            message.To.Add(to);

            await client.SendMailAsync(message, cancellationToken);
            _logger.LogInformation("Email '{Subject}' sent to {To}", subject, to);
            return true;
        }
        catch (Exception ex)
        {
            // Email must never break a business flow
            _logger.LogError(ex, "Failed to send email '{Subject}' to {To}", subject, to);
            return false;
        }
    }
}
