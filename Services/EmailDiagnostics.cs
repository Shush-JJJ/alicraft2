namespace Alicraft2.Services;

// Types used by BrevoEmailService.GetDiagnostics() and the (currently orphan)
// /Admin/EmailDiag view. Re-introduced after they went missing from the repo.

/// <summary>One row in the diagnostics table (label / value).</summary>
public record EmailDiagField(string Label, string Value);

/// <summary>
/// Snapshot of the live email-provider configuration shown on the admin
/// diagnostics page. The positional constructor matches what
/// <see cref="BrevoEmailService.GetDiagnostics"/> already creates; the extra
/// init-only properties are there so the older SMTP-style view template
/// (Host/Port/EnableSsl/etc.) keeps compiling even though the Brevo provider
/// surfaces those values through the generic <see cref="Fields"/> list instead.
/// </summary>
public record EmailDiagnostics(
    string Provider,
    bool IsRealSendConfigured,
    string MissingFields,
    string BaseUrl,
    List<EmailDiagField> Fields)
{
    // Legacy SMTP-shaped fields. Brevo provider leaves them at the defaults
    // and exposes the equivalent data through Fields instead.
    public string Host           { get; init; } = "(see Fields)";
    public int    Port           { get; init; } = 0;
    public bool   EnableSsl      { get; init; } = false;
    public string Username       { get; init; } = "";
    public string PasswordStatus { get; init; } = "";
    public string FromEmail      { get; init; } = "";
    public string FromName       { get; init; } = "";
}

/// <summary>Helpers for safely displaying secrets / addresses in diagnostics output.</summary>
public static class EmailMasking
{
    /// <summary>
    /// Mask an email so admins can verify "yes that's the right account" without
    /// leaking the full address into screenshots: <c>j***n@example.com</c>.
    /// </summary>
    public static string MaskEmail(string? email)
    {
        if (string.IsNullOrWhiteSpace(email)) return "(not set)";
        var at = email.IndexOf('@');
        if (at <= 0) return "***";

        var local  = email[..at];
        var domain = email[at..];

        if (local.Length <= 2) return $"{local[0]}***{domain}";
        return $"{local[0]}***{local[^1]}{domain}";
    }

    /// <summary>
    /// Never echo a secret back. Show only its length so an admin can confirm
    /// the value was loaded from configuration.
    /// </summary>
    public static string MaskSecret(string? secret)
        => string.IsNullOrWhiteSpace(secret) ? "(not set)" : $"({secret.Length} chars)";
}
