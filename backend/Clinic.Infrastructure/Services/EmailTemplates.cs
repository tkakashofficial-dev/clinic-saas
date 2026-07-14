using System.Net;

namespace Clinic.Infrastructure.Services;

/// <summary>
/// One branded layout for every email Klivia sends. Email clients ignore
/// stylesheets, so everything is inline and table-free-simple (Gmail-safe).
/// Values are HTML-encoded — never trust user-entered names in markup.
/// </summary>
public static class EmailTemplates
{
    private const string Ink = "#0C2B23";
    private const string Teal = "#00BD8F";
    private const string TealDark = "#008465";
    private const string Bg = "#F4FAF7";
    private const string Text = "#10201B";
    private const string Muted = "#5B6F68";
    private const string Border = "#E2EDE8";

    public const string SupportWhatsApp = "+91 62384 56205";
    public const string SupportWhatsAppLink = "https://wa.me/916238456205";

    /// <summary>Escapes user-supplied values before they enter HTML.</summary>
    public static string Safe(string value) => WebUtility.HtmlEncode(value);

    public static string Branded(
        string heading,
        string bodyHtml,
        string? ctaText = null,
        string? ctaUrl = null,
        string? footerNote = null)
    {
        var cta = ctaText is null || ctaUrl is null
            ? ""
            : $"""
              <div style="text-align:center;margin:28px 0 8px">
                <a href="{ctaUrl}"
                   style="display:inline-block;background:{Teal};color:#06362B;
                          font-weight:bold;font-size:15px;text-decoration:none;
                          padding:14px 32px;border-radius:10px">
                  {ctaText}
                </a>
              </div>
              <p style="text-align:center;font-size:12px;color:{Muted};margin:0 0 8px">
                Button not working? Copy this link:<br>
                <a href="{ctaUrl}" style="color:{TealDark};word-break:break-all">{ctaUrl}</a>
              </p>
              """;

        var note = footerNote is null
            ? ""
            : $"""
              <hr style="border:none;border-top:1px solid {Border};margin:24px 0 16px">
              <p style="font-size:12.5px;color:{Muted};margin:0">{footerNote}</p>
              """;

        return $"""
            <!doctype html>
            <html>
            <body style="margin:0;padding:0;background:{Bg}">
              <div style="background:{Bg};padding:32px 16px;
                          font-family:Arial,Helvetica,sans-serif">
                <div style="max-width:560px;margin:0 auto;background:#FFFFFF;
                            border-radius:16px;overflow:hidden;
                            border:1px solid {Border}">

                  <!-- Header band -->
                  <div style="background:{Ink};padding:20px 28px">
                    <span style="display:inline-block;background:{Teal};color:#06362B;
                                 font-weight:bold;font-size:16px;border-radius:8px;
                                 width:28px;height:28px;line-height:28px;
                                 text-align:center;vertical-align:middle">+</span>
                    <span style="color:#F4FAF7;font-size:19px;font-weight:bold;
                                 vertical-align:middle;margin-left:10px">Klivia</span>
                  </div>

                  <!-- Content -->
                  <div style="padding:30px 28px">
                    <h1 style="color:{Ink};font-size:21px;margin:0 0 16px">{heading}</h1>
                    <div style="color:{Text};font-size:14.5px;line-height:1.65">
                      {bodyHtml}
                    </div>
                    {cta}
                    {note}
                  </div>
                </div>

                <!-- Footer -->
                <p style="text-align:center;font-size:12px;color:{Muted};
                          margin:18px 0 0">
                  Klivia — clinic management, made calm<br>
                  Questions? WhatsApp us:
                  <a href="{SupportWhatsAppLink}" style="color:{TealDark}">{SupportWhatsApp}</a>
                </p>
              </div>
            </body>
            </html>
            """;
    }

    public static string Welcome(string firstName, string clinicName, string dashboardUrl) =>
        Branded(
            $"Welcome, {Safe(firstName)} 🎉",
            $"""
            <p style="margin:0 0 14px"><strong>{Safe(clinicName)}</strong> is set up and ready to run.</p>
            <p style="margin:0 0 8px">Your next three steps:</p>
            <table style="border-collapse:collapse;margin:0 0 6px">
              <tr><td style="padding:6px 10px 6px 0;color:{TealDark};font-weight:bold">1</td>
                  <td style="padding:6px 0">Add your doctors and reception staff</td></tr>
              <tr><td style="padding:6px 10px 6px 0;color:{TealDark};font-weight:bold">2</td>
                  <td style="padding:6px 0">Register your first patient</td></tr>
              <tr><td style="padding:6px 10px 6px 0;color:{TealDark};font-weight:bold">3</td>
                  <td style="padding:6px 0">Book an appointment and try the consultation flow</td></tr>
            </table>
            """,
            "Open your dashboard",
            dashboardUrl,
            "You're on a 14-day free trial with full features — no payment details needed.");

    public static string StaffInvite(
        string firstName, string clinicName, string roles, string setPasswordUrl,
        bool hasTempPassword) =>
        Branded(
            $"You've been added to {Safe(clinicName)} 👋",
            $"""
            <p style="margin:0 0 14px">Hi {Safe(firstName)},</p>
            <p style="margin:0 0 14px">
              You now have an account at <strong>{Safe(clinicName)}</strong> as
              <span style="background:#E9FBF5;color:{TealDark};font-weight:bold;
                           padding:3px 10px;border-radius:999px;font-size:13px">{Safe(roles)}</span>
            </p>
            <p style="margin:0">Create your account to get started:</p>
            """,
            "Create my account",
            setPasswordUrl,
            hasTempPassword
                ? "This link is valid for 7 days. You can also sign in with the temporary password your admin gave you, and change it later."
                : "This link is valid for 7 days. If it expires, ask your clinic admin to re-invite you.");

    /// <summary>Existing Klivia user attached to an additional clinic —
    /// their password stays the same; they just gain a clinic in the switcher.</summary>
    public static string AddedToClinic(
        string firstName, string clinicName, string roles, string loginUrl) =>
        Branded(
            $"{Safe(clinicName)} added you to their team 🏥",
            $"""
            <p style="margin:0 0 14px">Hi {Safe(firstName)},</p>
            <p style="margin:0 0 14px">
              Your Klivia account now also has access to
              <strong>{Safe(clinicName)}</strong> as
              <span style="background:#E9FBF5;color:{TealDark};font-weight:bold;
                           padding:3px 10px;border-radius:999px;font-size:13px">{Safe(roles)}</span>
            </p>
            <p style="margin:0">
              Sign in with your <strong>existing password</strong>, then use the
              clinic switcher (top of the sidebar) to move between your clinics.
            </p>
            """,
            "Sign in",
            loginUrl,
            "Didn't expect this? Contact the clinic's admin — your account and password are unchanged.");

    public static string PasswordReset(string firstName, string resetUrl, int validMinutes) =>
        Branded(
            "Reset your password",
            $"""
            <p style="margin:0 0 14px">Hi {Safe(firstName)},</p>
            <p style="margin:0">
              Click below to choose a new password. The link works
              <strong>once</strong> and expires in <strong>{validMinutes} minutes</strong>.
            </p>
            """,
            "Choose new password",
            resetUrl,
            "Didn't request this? You can safely ignore this email — your password stays unchanged.");
}
