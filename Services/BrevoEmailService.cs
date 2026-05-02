using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace Alicraft2.Services;

/// <summary>
/// Settings for Brevo (formerly Sendinblue) transactional email API.
/// FromEmail / FromName fall back to the Smtp settings if not specified, so
/// you don't have to repeat them.
/// </summary>
public class BrevoSettings
{
    public string ApiKey { get; set; } = "";
    public string FromEmail { get; set; } = "";
    public string FromName { get; set; } = "";
}

/// <summary>
/// IEmailService implementation that sends mail via the Brevo HTTP API
/// (https://api.brevo.com/v3/smtp/email). Uses port 443 (HTTPS) so it works
/// on hosts that block outbound SMTP ports — like Render's free tier.
/// </summary>
public class BrevoEmailService : IEmailService
{
    private const string BrevoEndpoint = "https://api.brevo.com/v3/smtp/email";

    private readonly BrevoSettings _settings;
    private readonly SmtpSettings _smtpFallback;
    private readonly ILogger<BrevoEmailService> _log;
    private readonly IHttpClientFactory _httpFactory;

    public BrevoEmailService(IConfiguration cfg, ILogger<BrevoEmailService> log, IHttpClientFactory httpFactory)
    {
        _settings = new BrevoSettings();
        cfg.GetSection("Brevo").Bind(_settings);
        _smtpFallback = new SmtpSettings();
        cfg.GetSection("Smtp").Bind(_smtpFallback);
        _log = log;
        _httpFactory = httpFactory;
    }

    private string FromEmail => string.IsNullOrWhiteSpace(_settings.FromEmail) ? _smtpFallback.FromEmail : _settings.FromEmail;
    private string FromName  => string.IsNullOrWhiteSpace(_settings.FromName)  ? _smtpFallback.FromName  : _settings.FromName;

    public bool IsRealSendConfigured =>
        !string.IsNullOrWhiteSpace(_settings.ApiKey) &&
        !string.IsNullOrWhiteSpace(FromEmail);

    public string BaseUrl => string.IsNullOrWhiteSpace(_smtpFallback.BaseUrl)
        ? "http://localhost:5080"
        : _smtpFallback.BaseUrl.TrimEnd('/');

    public EmailDiagnostics GetDiagnostics()
    {
        var missing = new List<string>();
        if (string.IsNullOrWhiteSpace(_settings.ApiKey)) missing.Add("Brevo__ApiKey");
        if (string.IsNullOrWhiteSpace(FromEmail))        missing.Add("Brevo__FromEmail (or Smtp__FromEmail)");

        var fields = new List<EmailDiagField>
        {
            new("Endpoint",         BrevoEndpoint),
            new("Brevo__ApiKey",    EmailMasking.MaskSecret(_settings.ApiKey)),
            new("From email",       EmailMasking.MaskEmail(FromEmail)),
            new("From name",        FromName),
            new("Smtp__BaseUrl",    BaseUrl)
        };

        return new EmailDiagnostics(
            Provider: "Brevo HTTP API (HTTPS, bypasses SMTP port blocks)",
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
            if (string.IsNullOrWhiteSpace(_settings.ApiKey)) missing.Add("Brevo__ApiKey");
            if (string.IsNullOrWhiteSpace(FromEmail))        missing.Add("FromEmail");
            var msg = $"Brevo not configured. Missing: {string.Join(", ", missing)}";
            _log.LogWarning("[BrevoEmailService] {Msg}. Would send to {To} | Subject: {Subject}",
                msg, toEmail, subject);
            return (true, msg);
        }

        _log.LogInformation(
            "[BrevoEmailService] POST {Endpoint} from {From} to {To}: {Subject}",
            BrevoEndpoint, FromEmail, toEmail, subject);

        try
        {
            var payload = new
            {
                sender = new { name = FromName, email = FromEmail },
                to = new[] { new { name = toName, email = toEmail } },
                subject,
                htmlContent = htmlBody
            };
            var json = JsonSerializer.Serialize(payload);

            var client = _httpFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(30);

            using var req = new HttpRequestMessage(HttpMethod.Post, BrevoEndpoint);
            req.Headers.Add("api-key", _settings.ApiKey);
            req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            req.Content = new StringContent(json, Encoding.UTF8, "application/json");

            using var resp = await client.SendAsync(req);
            var body = await resp.Content.ReadAsStringAsync();

            if (resp.IsSuccessStatusCode)
            {
                _log.LogInformation("Brevo email sent to {To}: {Subject} (HTTP {Status})", toEmail, subject, (int)resp.StatusCode);
                return (true, null);
            }

            var detail = $"Brevo HTTP {(int)resp.StatusCode} {resp.ReasonPhrase}: {Truncate(body, 500)}";
            _log.LogError("Failed to send via Brevo: {Detail}", detail);
            return (false, detail);
        }
        catch (TaskCanceledException ex)
        {
            var detail = $"Request timed out after 30s talking to Brevo. {ex.Message}";
            _log.LogError(ex, "Brevo HTTP send timed out: {Detail}", detail);
            return (false, detail);
        }
        catch (Exception ex)
        {
            var detail = $"{ex.GetType().Name}: {ex.Message}" +
                         (ex.InnerException != null ? $" | inner: {ex.InnerException.GetType().Name}: {ex.InnerException.Message}" : "");
            _log.LogError(ex, "Brevo HTTP send failed: {Detail}", detail);
            return (false, detail);
        }
    }

    private static string Truncate(string s, int max) => s.Length <= max ? s : s.Substring(0, max) + "…";
}
