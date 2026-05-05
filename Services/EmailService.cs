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

public interface IEmailService
{
    /// <summary>Send an email. Returns true if sent (or simulated). False if a real send failed.</summary>
    Task<bool> SendAsync(string toEmail, string toName, string subject, string htmlBody);

    /// <summary>True when SMTP credentials are configured. Otherwise the service runs in "log to console" mode.</summary>
    bool IsRealSendConfigured { get; }

    /// <summary>The base URL that should be used to construct absolute links inside email content.</summary>
    string BaseUrl { get; }
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

    public async Task<bool> SendAsync(string toEmail, string toName, string subject, string htmlBody)
    {
        if (!IsRealSendConfigured)
        {
            // Dev fallback: log the email to console so the developer can copy the verification link.
            _log.LogWarning(
                "[EmailService] SMTP not configured. Would send to {To} <{Name}> | Subject: {Subject}\n--- Body ---\n{Body}\n------------",
                toEmail, toName, subject, StripHtml(htmlBody));
            return true;
        }

        try
        {
            using var msg = new MailMessage();
            msg.From = new MailAddress(_settings.FromEmail, _settings.FromName);
            msg.To.Add(new MailAddress(toEmail, toName));
            msg.Subject = subject;
            msg.Body = htmlBody;
            msg.IsBodyHtml = true;

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

            await client.SendMailAsync(msg);
            _log.LogInformation("Email sent to {To}: {Subject}", toEmail, subject);
            return true;
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Failed to send email to {To}: {Subject}", toEmail, subject);
            return false;
        }
    }

    private static string StripHtml(string html)
    {
        if (string.IsNullOrEmpty(html)) return html;
        var noTags = System.Text.RegularExpressions.Regex.Replace(html, "<[^>]+>", " ");
        return System.Text.RegularExpressions.Regex.Replace(noTags, "\\s+", " ").Trim();
    }
}
