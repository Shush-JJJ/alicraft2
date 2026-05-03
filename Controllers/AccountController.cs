using System.Security.Claims;
using Alicraft2.Data;
using Alicraft2.Models;
using Alicraft2.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Alicraft2.Controllers;

public class ResendVerificationResult
{
    public string Message { get; set; } = "";
}

public class AccountController : Controller
{
    private readonly AppDbContext _db;
    private readonly CurrentUser _current;
    private readonly IWebHostEnvironment _env;
    private readonly IEmailService _email;
    private readonly ILogger<AccountController> _log;
    private readonly IConfiguration _config;

    public AccountController(AppDbContext db, CurrentUser current, IWebHostEnvironment env, IEmailService email, ILogger<AccountController> log, IConfiguration config)
    {
        _db = db;
        _current = current;
        _env = env;
        _email = email;
        _log = log;
        _config = config;
    }

    // When true (default), new accounts must click a verification link before logging in.
    // Override to false on hosts where email delivery is unreliable (e.g. Render free tier
    // with Brevo blocklisting) by setting env var: Account__RequireEmailVerification=false
    private bool RequireEmailVerification =>
        _config.GetValue("Account:RequireEmailVerification", true);

    // ---------- REGISTER ----------
    [HttpGet]
    public IActionResult Register() => View(new RegisterVm());

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(RegisterVm vm)
    {
        if (!ModelState.IsValid) return View(vm);

        var email = vm.Email.Trim().ToLowerInvariant();
        if (await _db.Users.AnyAsync(u => u.Email == email))
        {
            ModelState.AddModelError(nameof(vm.Email), "This email is already registered.");
            return View(vm);
        }

        if (!LocationData.ProvinceCities.ContainsKey(vm.Province) ||
            !LocationData.CitiesFor(vm.Province).Contains(vm.City))
        {
            ModelState.AddModelError(nameof(vm.City), "Please pick a valid Province and City.");
            return View(vm);
        }

        var requireVerify = RequireEmailVerification;
        var user = new User
        {
            Name = vm.Name.Trim(),
            Email = email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(vm.Password),
            Phone = vm.Phone.Trim(),
            Role = "User",
            Province = vm.Province,
            City = vm.City,
            Barangay = vm.Barangay,
            Street = vm.Street,
            PostalCode = vm.PostalCode,
            IsEmailVerified = !requireVerify,
            EmailVerificationToken = requireVerify ? NewToken() : null,
            EmailVerificationTokenExpiresAt = requireVerify ? DateTime.UtcNow.AddHours(24) : null,
            EmailVerificationLastSentAt = requireVerify ? DateTime.UtcNow : (DateTime?)null
        };
        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        // Demo mode: verification disabled. Account is already verified, skip the email.
        if (!requireVerify)
        {
            TempData["Success"] = $"Account created for {user.Email}. You can log in now.";
            return RedirectToAction(nameof(Login));
        }

        var (sent, sendError) = await SendVerificationEmailAsync(user);

        if (sent)
        {
            TempData["Success"] = $"We sent a verification link to {user.Email}. Please check your inbox (and Spam folder) to activate your account.";
        }
        else
        {
            _log.LogError("Verification email send failed for {Email}: {Error}", user.Email, sendError);
            var resendUrl = Url.Action(nameof(ResendVerification), new { email = user.Email });
            TempData["Warning"] =
                $"Your account was created, but we couldn't send the verification email right now. " +
                $"Reason: <code>{System.Net.WebUtility.HtmlEncode(sendError ?? "unknown error")}</code>. " +
                $"You can <a href=\"{resendUrl}\">click here to resend</a> in a moment.";
        }
        return RedirectToAction(nameof(VerifyEmailSent), new { email = user.Email });
    }

    // ---------- EMAIL VERIFICATION ----------
    [HttpGet]
    public IActionResult VerifyEmailSent(string? email)
    {
        ViewBag.Email = email ?? "";
        ViewBag.SmtpConfigured = _email.IsRealSendConfigured;
        return View();
    }

    [HttpGet]
    public async Task<IActionResult> VerifyEmail(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            ViewBag.Status = "invalid";
            return View();
        }
        var user = await _db.Users.FirstOrDefaultAsync(u => u.EmailVerificationToken == token);
        if (user == null)
        {
            ViewBag.Status = "invalid";
            return View();
        }
        if (user.IsEmailVerified)
        {
            ViewBag.Status = "already";
            ViewBag.Email = user.Email;
            return View();
        }
        if (user.EmailVerificationTokenExpiresAt < DateTime.UtcNow)
        {
            ViewBag.Status = "expired";
            ViewBag.Email = user.Email;
            return View();
        }

        user.IsEmailVerified = true;
        user.EmailVerificationToken = null;
        user.EmailVerificationTokenExpiresAt = null;
        await _db.SaveChangesAsync();

        ViewBag.Status = "ok";
        ViewBag.Email = user.Email;
        return View();
    }

    [HttpGet]
    public IActionResult ResendVerification(string? email)
    {
        ViewBag.Email = email ?? "";
        return View(model: new ResendVerificationResult());
    }

    [HttpPost, ValidateAntiForgeryToken, ActionName("ResendVerification")]
    public async Task<IActionResult> ResendVerificationPost(string email)
    {
        ViewBag.Email = email ?? "";
        var result = new ResendVerificationResult();
        var normalized = (email ?? "").Trim().ToLowerInvariant();
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == normalized);

        // Always show same message to avoid leaking whether the email exists
        result.Message = $"If an account with that email exists and is not yet verified, we've sent a new verification link.";

        if (user != null && !user.IsEmailVerified)
        {
            // Throttle: at most one resend per 60 seconds
            if (user.EmailVerificationLastSentAt.HasValue &&
                DateTime.UtcNow - user.EmailVerificationLastSentAt.Value < TimeSpan.FromSeconds(60))
            {
                result.Message = "Please wait a minute before requesting another verification email.";
            }
            else
            {
                user.EmailVerificationToken = NewToken();
                user.EmailVerificationTokenExpiresAt = DateTime.UtcNow.AddHours(24);
                user.EmailVerificationLastSentAt = DateTime.UtcNow;
                await _db.SaveChangesAsync();
                var (sent, sendError) = await SendVerificationEmailAsync(user);
                if (!sent)
                {
                    _log.LogError("Resend verification failed for {Email}: {Error}", user.Email, sendError);
                    result.Message = $"We couldn't send the verification email. Reason: {sendError}. Please try again in a minute.";
                }
            }
        }
        return View("ResendVerification", result);
    }

    private async Task<(bool ok, string? error)> SendVerificationEmailAsync(User user)
    {
        if (string.IsNullOrEmpty(user.EmailVerificationToken)) return (false, "missing token");
        var baseUrl = _email.BaseUrl;
        var link = $"{baseUrl}/Account/VerifyEmail?token={Uri.EscapeDataString(user.EmailVerificationToken)}";
        var html = $@"<!doctype html><html><body style='font-family:Arial,sans-serif;background:#f7f7f9;padding:24px;'>
<div style='max-width:560px;margin:0 auto;background:#fff;border-radius:12px;padding:32px;border:1px solid #e5e7eb;'>
  <h2 style='margin-top:0;color:#7A3FA5;'>Welcome to AliCraft Creations!</h2>
  <p>Hi {System.Net.WebUtility.HtmlEncode(user.Name)}, please confirm your email address to activate your account.</p>
  <p style='text-align:center;margin:28px 0;'>
    <a href='{link}' style='background:linear-gradient(135deg,#E91E63,#7A3FA5,#2196F3);color:#fff;text-decoration:none;padding:14px 28px;border-radius:8px;font-weight:700;display:inline-block;'>Verify my email</a>
  </p>
  <p style='font-size:14px;color:#6b7280;'>Or paste this link in your browser:<br><span style='word-break:break-all;color:#374151;'>{link}</span></p>
  <p style='font-size:13px;color:#6b7280;margin-top:24px;'>This link expires in 24 hours. If you didn't sign up, you can ignore this email.</p>
  <hr style='border:none;border-top:1px solid #e5e7eb;margin:24px 0;'>
  <p style='font-size:12px;color:#9ca3af;text-align:center;'>AliCraft Creations &middot; Custom 3D Lithophane Crafts</p>
</div></body></html>";
        return await _email.TrySendAsync(user.Email, user.Name, "Confirm your AliCraft email", html);
    }

    private static string NewToken() => Convert.ToBase64String(Guid.NewGuid().ToByteArray()).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    // ---------- LOGIN ----------
    [HttpGet]
    public IActionResult Login(string? returnUrl = null)
    {
        ViewData["ReturnUrl"] = returnUrl;
        return View(new LoginVm());
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginVm vm, string? returnUrl = null)
    {
        ViewData["ReturnUrl"] = returnUrl;
        if (!ModelState.IsValid) return View(vm);

        var email = vm.Email.Trim().ToLowerInvariant();
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == email);
        if (user == null || !BCrypt.Net.BCrypt.Verify(vm.Password, user.PasswordHash))
        {
            ModelState.AddModelError(string.Empty, "Incorrect email or password.");
            return View(vm);
        }

        if (RequireEmailVerification && !user.IsEmailVerified)
        {
            ViewBag.UnverifiedEmail = user.Email;
            ModelState.AddModelError(string.Empty, "Please verify your email before logging in. Check your inbox for the verification link.");
            return View(vm);
        }

        await SignInAsync(user, vm.Remember);

        if (user.Role == "Admin")
            return RedirectToAction("Index", "Admin");

        if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            return Redirect(returnUrl);
        return RedirectToAction("Index", "Home");
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction("Index", "Home");
    }

    // ---------- FORGOT PASSWORD (email link) ----------
    [HttpGet]
    public IActionResult ForgotPassword() => View(new ForgotPasswordVm());

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ForgotPassword(ForgotPasswordVm vm)
    {
        if (!ModelState.IsValid) return View(vm);

        var email = vm.Email.Trim().ToLowerInvariant();
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == email);

        // Only send a real email when the account exists. Either way we redirect to the
        // same "check your email" page so we don't leak which addresses are registered.
        if (user != null)
        {
            // Throttle: at most one reset email per 60 seconds
            var canSend = !user.PasswordResetLastSentAt.HasValue ||
                          DateTime.UtcNow - user.PasswordResetLastSentAt.Value >= TimeSpan.FromSeconds(60);
            if (canSend)
            {
                user.PasswordResetToken = NewToken();
                user.PasswordResetExpiresAt = DateTime.UtcNow.AddHours(1);
                user.PasswordResetLastSentAt = DateTime.UtcNow;
                await _db.SaveChangesAsync();
                await SendPasswordResetEmailAsync(user);
            }
        }

        return RedirectToAction(nameof(ForgotPasswordSent), new { email });
    }

    [HttpGet]
    public IActionResult ForgotPasswordSent(string? email)
    {
        ViewBag.Email = email ?? "";
        ViewBag.SmtpConfigured = _email.IsRealSendConfigured;
        return View();
    }

    [HttpGet]
    public async Task<IActionResult> ResetPassword(string? token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            ViewBag.Status = "invalid";
            return View(new ResetPasswordVm());
        }
        var user = await _db.Users.FirstOrDefaultAsync(u => u.PasswordResetToken == token);
        if (user == null)
        {
            ViewBag.Status = "invalid";
            return View(new ResetPasswordVm());
        }
        if (user.PasswordResetExpiresAt < DateTime.UtcNow)
        {
            ViewBag.Status = "expired";
            ViewBag.Email = user.Email;
            return View(new ResetPasswordVm());
        }

        ViewBag.Status = "ok";
        ViewBag.Email = user.Email;
        return View(new ResetPasswordVm { Token = token });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ResetPassword(ResetPasswordVm vm)
    {
        // Re-validate the token first regardless of model state
        var user = string.IsNullOrWhiteSpace(vm.Token)
            ? null
            : await _db.Users.FirstOrDefaultAsync(u => u.PasswordResetToken == vm.Token);

        if (user == null)
        {
            ViewBag.Status = "invalid";
            return View(vm);
        }
        if (user.PasswordResetExpiresAt < DateTime.UtcNow)
        {
            ViewBag.Status = "expired";
            ViewBag.Email = user.Email;
            return View(vm);
        }

        if (!ModelState.IsValid)
        {
            ViewBag.Status = "ok";
            ViewBag.Email = user.Email;
            return View(vm);
        }

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(vm.NewPassword);
        // Single-use token: clear it after a successful reset
        user.PasswordResetToken = null;
        user.PasswordResetExpiresAt = null;
        // Convenience: a successful password reset also confirms the user controls the inbox,
        // so we treat the email as verified.
        user.IsEmailVerified = true;
        user.EmailVerificationToken = null;
        user.EmailVerificationTokenExpiresAt = null;
        await _db.SaveChangesAsync();

        TempData["Success"] = "Password reset successful. You can now log in with your new password.";
        return RedirectToAction(nameof(Login));
    }

    private async Task SendPasswordResetEmailAsync(User user)
    {
        if (string.IsNullOrEmpty(user.PasswordResetToken)) return;
        var link = $"{_email.BaseUrl}/Account/ResetPassword?token={Uri.EscapeDataString(user.PasswordResetToken)}";
        var html = $@"<!doctype html><html><body style='font-family:Arial,sans-serif;background:#f7f7f9;padding:24px;'>
<div style='max-width:560px;margin:0 auto;background:#fff;border-radius:12px;padding:32px;border:1px solid #e5e7eb;'>
  <h2 style='margin-top:0;color:#7A3FA5;'>Reset your AliCraft password</h2>
  <p>Hi {System.Net.WebUtility.HtmlEncode(user.Name)}, we received a request to reset your password. Click the button below to choose a new one.</p>
  <p style='text-align:center;margin:28px 0;'>
    <a href='{link}' style='background:linear-gradient(135deg,#E91E63,#7A3FA5,#2196F3);color:#fff;text-decoration:none;padding:14px 28px;border-radius:8px;font-weight:700;display:inline-block;'>Reset my password</a>
  </p>
  <p style='font-size:14px;color:#6b7280;'>Or paste this link in your browser:<br><span style='word-break:break-all;color:#374151;'>{link}</span></p>
  <p style='font-size:13px;color:#6b7280;margin-top:24px;'>This link expires in 1 hour. If you didn't request this, you can safely ignore this email — your password won't change.</p>
  <hr style='border:none;border-top:1px solid #e5e7eb;margin:24px 0;'>
  <p style='font-size:12px;color:#9ca3af;text-align:center;'>AliCraft Creations &middot; Custom 3D Lithophane Crafts</p>
</div></body></html>";
        await _email.SendAsync(user.Email, user.Name, "Reset your AliCraft password", html);
    }

    // ---------- PROFILE ----------
    [Authorize]
    [HttpGet]
    public async Task<IActionResult> Profile()
    {
        var user = await _current.GetAsync();
        if (user == null) return RedirectToAction(nameof(Login));
        var vm = new ProfileVm
        {
            Name = user.Name,
            Email = user.Email,
            Phone = user.Phone,
            Province = user.Province,
            City = user.City,
            Barangay = user.Barangay,
            Street = user.Street,
            PostalCode = user.PostalCode
        };
        ViewBag.AvatarPath = user.AvatarPath;
        return View(vm);
    }

    [Authorize]
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Profile(ProfileVm vm)
    {
        var user = await _current.GetAsync();
        if (user == null) return RedirectToAction(nameof(Login));

        if (!ModelState.IsValid)
        {
            ViewBag.AvatarPath = user.AvatarPath;
            return View(vm);
        }

        user.Name = vm.Name.Trim();
        user.Phone = vm.Phone.Trim();
        user.Province = vm.Province;
        user.City = vm.City;
        user.Barangay = vm.Barangay;
        user.Street = vm.Street;
        user.PostalCode = vm.PostalCode;

        if (vm.Avatar != null && vm.Avatar.Length > 0)
        {
            var saved = await FileHelper.SaveAsync(_env, vm.Avatar, "avatars");
            if (saved != null) user.AvatarPath = saved;
        }

        await _db.SaveChangesAsync();

        // Refresh auth claims (in case name changed)
        await SignInAsync(user, remember: true);

        TempData["Success"] = "Profile updated successfully.";
        return RedirectToAction(nameof(Profile));
    }

    // ---------- CHANGE PASSWORD ----------
    [Authorize]
    [HttpGet]
    public IActionResult ChangePassword() => View(new ChangePasswordVm());

    [Authorize]
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangePassword(ChangePasswordVm vm)
    {
        if (!ModelState.IsValid) return View(vm);

        var user = await _current.GetAsync();
        if (user == null) return RedirectToAction(nameof(Login));

        if (!BCrypt.Net.BCrypt.Verify(vm.CurrentPassword, user.PasswordHash))
        {
            ModelState.AddModelError(nameof(vm.CurrentPassword), "Current password is incorrect.");
            return View(vm);
        }

        if (BCrypt.Net.BCrypt.Verify(vm.NewPassword, user.PasswordHash))
        {
            ModelState.AddModelError(nameof(vm.NewPassword), "New password must be different from current password.");
            return View(vm);
        }

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(vm.NewPassword);
        await _db.SaveChangesAsync();

        TempData["Success"] = "Password changed successfully.";
        return RedirectToAction(nameof(Profile));
    }

    private async Task SignInAsync(User user, bool remember)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.Name),
            new(ClaimTypes.Email, user.Email),
            new(ClaimTypes.Role, user.Role)
        };
        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var props = new AuthenticationProperties
        {
            IsPersistent = remember,
            ExpiresUtc = DateTimeOffset.UtcNow.AddDays(remember ? 14 : 1)
        };
        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(identity), props);
    }
}

public static class FileHelper
{
    public static readonly string[] AllowedImageExts = { ".jpg", ".jpeg", ".png", ".webp", ".gif" };

    public static async Task<string?> SaveAsync(IWebHostEnvironment env, IFormFile file, string subfolder)
    {
        if (file == null || file.Length == 0) return null;
        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!AllowedImageExts.Contains(ext)) return null;
        if (file.Length > 8 * 1024 * 1024) return null; // 8MB cap

        var folder = Path.Combine(env.WebRootPath, "uploads", subfolder);
        Directory.CreateDirectory(folder);
        var name = $"{Guid.NewGuid():N}{ext}";
        var path = Path.Combine(folder, name);
        await using var fs = System.IO.File.Create(path);
        await file.CopyToAsync(fs);
        return $"/uploads/{subfolder}/{name}";
    }
}
