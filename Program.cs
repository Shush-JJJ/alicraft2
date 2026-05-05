using Alicraft2.Data;
using Alicraft2.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("Default")
                      ?? "Data Source=alicraft2.db";

// Database provider selection:
// - If the connection string looks like Postgres (URL form "postgresql://..." or
//   key/value form "Host=..."), use Npgsql. That's what Render uses with Neon /
//   Supabase so the data survives restarts.
// - Otherwise treat it as SQLite (default for local development).
static bool LooksLikePostgres(string cs)
    => cs.Contains("postgresql://", StringComparison.OrdinalIgnoreCase)
    || cs.Contains("postgres://",   StringComparison.OrdinalIgnoreCase)
    || cs.Contains("Host=",         StringComparison.OrdinalIgnoreCase);

// Npgsql only understands key/value format (Host=...;Database=...). Many
// providers (Neon, Supabase, Heroku) hand out URL-style connection strings
// (postgresql://user:pass@host:port/db?sslmode=require). Convert them so users
// can paste either form into the env var without thinking about it.
static string NormalizePostgresConnectionString(string cs)
{
    if (!cs.StartsWith("postgres://",   StringComparison.OrdinalIgnoreCase) &&
        !cs.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase))
    {
        return cs; // already key=value form
    }

    var uri = new Uri(cs);
    var userInfo = uri.UserInfo.Split(':', 2);

    var parts = new List<string>
    {
        $"Host={uri.Host}"
    };
    if (uri.Port > 0)
        parts.Add($"Port={uri.Port}");
    parts.Add($"Database={Uri.UnescapeDataString(uri.AbsolutePath.TrimStart('/'))}");
    if (userInfo.Length > 0 && !string.IsNullOrEmpty(userInfo[0]))
        parts.Add($"Username={Uri.UnescapeDataString(userInfo[0])}");
    if (userInfo.Length > 1 && !string.IsNullOrEmpty(userInfo[1]))
        parts.Add($"Password={Uri.UnescapeDataString(userInfo[1])}");

    foreach (var pair in uri.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
    {
        var kv = pair.Split('=', 2);
        if (kv.Length == 0 || string.IsNullOrEmpty(kv[0])) continue;
        var key   = kv[0];
        var value = kv.Length > 1 ? Uri.UnescapeDataString(kv[1]) : "";

        // Translate libpq sslmode values to Npgsql's "SSL Mode" enum names.
        if (string.Equals(key, "sslmode", StringComparison.OrdinalIgnoreCase))
        {
            var npgsqlMode = value.ToLowerInvariant() switch
            {
                "disable"     => "Disable",
                "allow"       => "Allow",
                "prefer"      => "Prefer",
                "require"     => "Require",
                "verify-ca"   => "VerifyCA",
                "verify-full" => "VerifyFull",
                _             => value
            };
            parts.Add($"SSL Mode={npgsqlMode}");
        }
        else
        {
            parts.Add($"{key}={value}");
        }
    }

    // Neon (and most managed Postgres) use certs from a CA Npgsql doesn't
    // ship by default. TrustServerCertificate=true skips the CA check while
    // still encrypting the connection.
    if (!parts.Any(p => p.StartsWith("Trust Server Certificate", StringComparison.OrdinalIgnoreCase)))
        parts.Add("Trust Server Certificate=true");

    return string.Join(';', parts) + ";";
}

builder.Services.AddDbContext<AppDbContext>(opt =>
{
    if (LooksLikePostgres(connectionString))
        opt.UseNpgsql(NormalizePostgresConnectionString(connectionString));
    else
        opt.UseSqlite(connectionString);
});

builder.Services
    .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.AccessDeniedPath = "/Account/Login";
        options.ExpireTimeSpan = TimeSpan.FromDays(14);
        options.SlidingExpiration = true;
        options.Cookie.Name = "Alicraft2.Auth";
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminOnly", p => p.RequireRole("Admin"));
});

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<CurrentUser>();

// Email provider selection:
// - If a Brevo API key is configured (Brevo__ApiKey on Render), use the Brevo
//   HTTP API. It talks over HTTPS port 443 so it works on hosts that block
//   outbound SMTP (e.g. Render's free tier).
// - Otherwise fall back to System.Net.Mail SMTP (good for local dev with Gmail
//   App Passwords or any other SMTP server).
var brevoApiKey = builder.Configuration["Brevo:ApiKey"];
if (!string.IsNullOrWhiteSpace(brevoApiKey))
{
    builder.Services.AddHttpClient();
    builder.Services.AddSingleton<IEmailService, BrevoEmailService>();
}
else
{
    builder.Services.AddSingleton<IEmailService, EmailService>();
}

builder.Services.AddControllersWithViews();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await DbInitializer.SeedAsync(db);
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
}

app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
