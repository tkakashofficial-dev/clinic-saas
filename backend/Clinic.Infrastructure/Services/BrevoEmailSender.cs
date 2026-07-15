using Clinic.Application.Common.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Net.Http.Json;

namespace Clinic.Infrastructure.Services;

public class BrevoSettings
{
    /// <summary>API key from brevo.com → SMTP & API → API keys. Free tier: 300 mails/day.</summary>
    public string ApiKey { get; set; } = "";
    /// <summary>Must be a sender verified in Brevo (a single email is enough, no domain needed).</summary>
    public string FromEmail { get; set; } = "";
    public string FromName { get; set; } = "Klivia";

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(ApiKey) && !string.IsNullOrWhiteSpace(FromEmail);
}

/// <summary>
/// Email over HTTPS via Brevo's API — hosting providers never block HTTP the
/// way they sometimes throttle SMTP, and there are no Gmail app passwords to
/// expire. Selected automatically over SMTP when configured (see DI).
/// </summary>
public class BrevoEmailSender : IEmailSender
{
    private readonly HttpClient _http;
    private readonly BrevoSettings _settings;
    private readonly ILogger<BrevoEmailSender> _logger;

    public BrevoEmailSender(
        HttpClient http, IOptions<BrevoSettings> settings, ILogger<BrevoEmailSender> logger)
    {
        _http = http;
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task<bool> SendAsync(
        string to, string subject, string htmlBody, CancellationToken cancellationToken = default)
    {
        if (!_settings.IsConfigured)
        {
            _logger.LogInformation("Brevo not configured — skipping '{Subject}' to {To}", subject, to);
            return false;
        }

        try
        {
            using var request = new HttpRequestMessage(
                HttpMethod.Post, "https://api.brevo.com/v3/smtp/email");
            request.Headers.Add("api-key", _settings.ApiKey);
            request.Content = JsonContent.Create(new
            {
                sender = new { name = _settings.FromName, email = _settings.FromEmail },
                to = new[] { new { email = to } },
                subject,
                htmlContent = htmlBody
            });

            using var response = await _http.SendAsync(request, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("Email '{Subject}' sent to {To} via Brevo", subject, to);
                return true;
            }

            var detail = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogError("Brevo rejected '{Subject}' to {To}: {Status} {Detail}",
                subject, to, (int)response.StatusCode, detail);
            return false;
        }
        catch (Exception ex)
        {
            // Email must never break a business flow
            _logger.LogError(ex, "Failed to send email '{Subject}' to {To} via Brevo", subject, to);
            return false;
        }
    }
}
