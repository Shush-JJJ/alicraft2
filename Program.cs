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
