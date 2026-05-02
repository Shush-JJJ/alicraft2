using Alicraft2.Data;
using Alicraft2.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("Default")
                      ?? "Data Source=alicraft2.db";

builder.Services.AddDbContext<AppDbContext>(opt => opt.UseSqlite(connectionString));

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
builder.Services.AddHttpClient();

// Email provider selection: if Brevo__ApiKey is configured (works on hosts that
// block outbound SMTP, like Render free tier), use the Brevo HTTP API.
// Otherwise fall back to System.Net.Mail SMTP — fine for local dev with Gmail.
builder.Services.AddSingleton<IEmailService>(sp =>
{
    var cfg = sp.GetRequiredService<IConfiguration>();
    var brevoKey = cfg["Brevo:ApiKey"];
    if (!string.IsNullOrWhiteSpace(brevoKey))
    {
        return new BrevoEmailService(
            cfg,
            sp.GetRequiredService<ILogger<BrevoEmailService>>(),
            sp.GetRequiredService<IHttpClientFactory>());
    }
    return new EmailService(cfg, sp.GetRequiredService<ILogger<EmailService>>());
});

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
