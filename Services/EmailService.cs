#pragma warning disable SYSLIB0014  // System.Net.Mail.SmtpClient is functional in .NET 10 and adequate for our needs.
using System.Net;
using System.Net.Mail;

namespace Alicraft2.Services;

public class SmtpSettings
{
    public string Host { get; set; } = "";
    public int Port { get; set; } = 587;
    public bool EnableSsl { get; set; } = true;
    public string Username { get; set; } = "";
    public string Password { get; set; } = "";
    public string FromEmail { get; set; } = "";
    public string FromName { get; set; } = "AliCraft Creations";
    public string BaseUrl { get; set; } = "http://localhost:5080";
}

public record EmailDiagField(string Key, string Value);

public record EmailDiagnostics(
    string Provider,                              // e.g. "SMTP (Gmail)" or "Brevo HTTP API"
    bool IsRealSendConfigured,
    string MissingFields,                         // comma-joined env-var names, empty if none
    string BaseUrl,
    IReadOnlyList<EmailDiagField> Fields          // per-provider fields, in display order
);

public interface IEmailService
{
    /// <summary>Send an email. Returns true if sent (or simulated). False if a real send failed.</summary>
    Task<bool> SendAsync(string toEmail, string toName, string subject, string htmlBody);

    /// <summary>True when SMTP credentials are configured. Otherwise the service runs in "log to console" mode.</summary>
    bool IsRealSendConfigured { get; }

    /// <summary>The base URL that should be used to construct absolute links inside email content.</summary>
    string BaseUrl { get; }

    /// <summary>Returns a snapshot of the current SMTP settings with sensitive values masked, for admin diagnostics.</summary>
    EmailDiagnostics GetDiagnostics();

    /// <summary>Same as SendAsync, but returns the SMTP exception message on failure for diagnostic display.</summary>
    Task<(bool ok, string? error)> TrySendAsync(string toEmail, string toName, string subject, string htmlBody);
}

public class EmailService : IEmailService
{
    private readonly SmtpSettings _settings;
    private readonly ILogger<EmailService> _log;

    public EmailService(IConfiguration cfg, ILogger<EmailService> log)
    {
        _settings = new SmtpSettings();
        cfg.GetSection("Smtp").Bind(_settings);
        _log = log;
    }

    public bool IsRealSendConfigured =>
        !string.IsNullOrWhiteSpace(_settings.Host) &&
        !string.IsNullOrWhiteSpace(_settings.Username) &&
        !string.IsNullOrWhiteSpace(_settings.Password) &&
        !string.IsNullOrWhiteSpace(_settings.FromEmail);

    public string BaseUrl => string.IsNullOrWhiteSpace(_settings.BaseUrl)
        ? "http://localhost:5080"
        : _settings.BaseUrl.TrimEnd('/');

    public EmailDiagnostics GetDiagnostics()
    {
        var missing = new List<string>();
        if (string.IsNullOrWhiteSpace(_settings.Host))      missing.Add("Smtp__Host");
        if (string.IsNullOrWhiteSpace(_settings.Username))  missing.Add("Smtp__Username");
        if (string.IsNullOrWhiteSpace(_settings.Password))  missing.Add("Smtp__Password");
        if (string.IsNullOrWhiteSpace(_settings.FromEmail)) missing.Add("Smtp__FromEmail");

        var pwStatus = string.IsNullOrEmpty(_settings.Password)
            ? "MISSING"
            : $"set (length={_settings.Password.Length}, no whitespace stripped)";

        var fields = new List<EmailDiagField>
        {
            new("Smtp__Host",      _settings.Host),
            new("Smtp__Port",      _settings.Port.ToString()),
            new("Smtp__EnableSsl", _settings.EnableSsl.ToString()),
            new("Smtp__Username",  EmailMasking.MaskEmail(_settings.Username)),
            new("Smtp__Password",  pwStatus),
            new("Smtp__FromEmail", EmailMasking.MaskEmail(_settings.FromEmail)),
            new("Smtp__FromName",  _settings.FromName),
            new("Smtp__BaseUrl",   BaseUrl)
        };

        return new EmailDiagnostics(
            Provider: "SMTP (System.Net.Mail)",
            IsRealSendConfigured: IsRealSendConfigured,
            MissingFields: string.Join(", ", missing),
            BaseUrl: BaseUrl,
            Fields: fields
        );
    }

    public async Task<bool> SendAsync(string toEmail, string toName, string subject, string htmlBody)
    {
        var (ok, _) = await TrySendAsync(toEmail, toName, subject, htmlBody);
        return ok;
    }

    public async Task<(bool ok, string? error)> TrySendAsync(string toEmail, string toName, string subject, string htmlBody)
    {
        if (!IsRealSendConfigured)
        {
            var missing = new List<string>();
            if (string.IsNullOrWhiteSpace(_settings.Host))      missing.Add("Smtp__Host");
            if (string.IsNullOrWhiteSpace(_settings.Username))  missing.Add("Smtp__Username");
            if (string.IsNullOrWhiteSpace(_settings.Password))  missing.Add("Smtp__Password");
            if (string.IsNullOrWhiteSpace(_settings.FromEmail)) missing.Add("Smtp__FromEmail");
            var msg = $"SMTP not configured. Missing: {string.Join(", ", missing)}";
            _log.LogWarning(
                "[EmailService] {Msg}. Would send to {To} <{Name}> | Subject: {Subject}\n--- Body ---\n{Body}\n------------",
                msg, toEmail, toName, subject, StripHtml(htmlBody));
            // SendAsync historically returned true here so the user-facing flow continues; preserve that.
            return (true, msg);
        }

        _log.LogInformation(
            "[EmailService] Sending email via {Host}:{Port} (ssl={Ssl}) from {From} to {To}: {Subject}",
            _settings.Host, _settings.Port, _settings.EnableSsl, _settings.FromEmail, toEmail, subject);

        try
        {
            using var mailMsg = new MailMessage();
            mailMsg.From = new MailAddress(_settings.FromEmail, _settings.FromName);
            mailMsg.To.Add(new MailAddress(toEmail, toName));
            mailMsg.Subject = subject;
            mailMsg.Body = htmlBody;
            mailMsg.IsBodyHtml = true;

            using var client = new SmtpClient(_settings.Host, _settings.Port)
            {
                EnableSsl = _settings.EnableSsl,
                DeliveryMethod = SmtpDeliveryMethod.Network,
                UseDefaultCredentials = false,
                Credentials = new NetworkCredential(_settings.Username, _settings.Password),
                // Some residential networks (especially IPv6-first) take 20+ seconds just to
                // establish the TCP connection to smtp.gmail.com. Give it plenty of headroom.
                Timeout = 60_000
            };

            await client.SendMailAsync(mailMsg);
            _log.LogInformation("Email sent to {To}: {Subject}", toEmail, subject);
            return (true, null);
        }
        catch (SmtpException ex)
        {
            var detail = $"{ex.GetType().Name}: {ex.Message}" +
                         (ex.StatusCode != 0 ? $" (status={ex.StatusCode})" : "") +
                         (ex.InnerException != null ? $" | inner: {ex.InnerException.GetType().Name}: {ex.InnerException.Message}" : "");
            _log.LogError(ex, "Failed to send email to {To}: {Subject}. {Detail}", toEmail, subject, detail);
            return (false, detail);
        }
        catch (Exception ex)
        {
            var detail = $"{ex.GetType().Name}: {ex.Message}" +
                         (ex.InnerException != null ? $" | inner: {ex.InnerException.GetType().Name}: {ex.InnerException.Message}" : "");
            _log.LogError(ex, "Failed to send email to {To}: {Subject}. {Detail}", toEmail, subject, detail);
            return (false, detail);
        }
    }

    private static string StripHtml(string html)
    {
        if (string.IsNullOrEmpty(html)) return html;
        var noTags = System.Text.RegularExpressions.Regex.Replace(html, "<[^>]+>", " ");
        return System.Text.RegularExpressions.Regex.Replace(noTags, "\\s+", " ").Trim();
    }
}

internal static class EmailMasking
{
    public static string MaskEmail(string s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        var at = s.IndexOf('@');
        if (at <= 0) return new string('*', s.Length);
        var local = s.Substring(0, at);
        var domain = s.Substring(at);
        var keep = local.Length <= 2 ? local : local.Substring(0, 2);
        return keep + new string('*', Math.Max(0, local.Length - keep.Length)) + domain;
    }

    public static string MaskSecret(string s)
    {
        if (string.IsNullOrEmpty(s)) return "MISSING";
        var visible = s.Length <= 8 ? 0 : 4;
        var hidden = s.Length - visible;
        return $"set (length={s.Length}, prefix={s.Substring(0, Math.Min(visible, s.Length))}{(hidden > 0 ? new string('*', Math.Min(hidden, 12)) : "")})";
    }
}
