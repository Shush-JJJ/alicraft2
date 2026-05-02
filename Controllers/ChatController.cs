using Alicraft2.Data;
using Alicraft2.Models;
using Alicraft2.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Alicraft2.Controllers;

[Authorize]
public class ChatController : Controller
{
    private readonly AppDbContext _db;
    private readonly CurrentUser _current;
    private readonly IWebHostEnvironment _env;

    public ChatController(AppDbContext db, CurrentUser current, IWebHostEnvironment env)
    {
        _db = db;
        _current = current;
        _env = env;
    }

    // User view of their chat with admin
    public async Task<IActionResult> Index()
    {
        if (_current.IsAdmin) return RedirectToAction("Inbox", "Admin");

        var uid = _current.Id!.Value;
        var messages = await _db.ChatMessages
            .Where(m => m.ThreadUserId == uid)
            .OrderBy(m => m.CreatedAt)
            .ToListAsync();

        // Mark admin messages as read
        var unread = messages.Where(m => m.SenderRole == "Admin" && !m.IsRead).ToList();
        foreach (var m in unread) m.IsRead = true;
        if (unread.Any()) await _db.SaveChangesAsync();

        return View(messages);
    }

    // Admin messaging a specific user
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> Thread(int userId)
    {
        var user = await _db.Users.FindAsync(userId);
        if (user == null) return NotFound();

        var messages = await _db.ChatMessages
            .Where(m => m.ThreadUserId == userId)
            .OrderBy(m => m.CreatedAt)
            .ToListAsync();

        var unread = messages.Where(m => m.SenderRole == "User" && !m.IsRead).ToList();
        foreach (var m in unread) m.IsRead = true;
        if (unread.Any()) await _db.SaveChangesAsync();

        ViewBag.ThreadUser = user;
        return View("AdminThread", messages);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Send(int? userId, string? body, IFormFile? image)
    {
        var senderId = _current.Id!.Value;
        var role = _current.Role ?? "User";

        int threadUserId;
        if (role == "Admin")
        {
            if (userId == null) return BadRequest("userId is required for admin.");
            threadUserId = userId.Value;
        }
        else
        {
            threadUserId = senderId;
        }

        if (string.IsNullOrWhiteSpace(body) && (image == null || image.Length == 0))
        {
            TempData["Error"] = "Message cannot be empty.";
            return role == "Admin"
                ? RedirectToAction(nameof(Thread), new { userId = threadUserId })
                : RedirectToAction(nameof(Index));
        }

        string? imgPath = null;
        if (image != null && image.Length > 0)
            imgPath = await FileHelper.SaveAsync(_env, image, "chat");

        _db.ChatMessages.Add(new ChatMessage
        {
            ThreadUserId = threadUserId,
            SenderId = senderId,
            SenderRole = role,
            Body = body?.Trim() ?? string.Empty,
            ImagePath = imgPath
        });
        await _db.SaveChangesAsync();

        return role == "Admin"
            ? RedirectToAction(nameof(Thread), new { userId = threadUserId })
            : RedirectToAction(nameof(Index));
    }

    // Polling endpoint for near-real-time updates
    [HttpGet]
    public async Task<IActionResult> Poll(int? userId, long since = 0)
    {
        var role = _current.Role ?? "User";
        int threadUserId = role == "Admin" ? (userId ?? 0) : _current.Id!.Value;
        if (threadUserId <= 0) return Json(Array.Empty<object>());

        var sinceDate = since > 0
            ? DateTimeOffset.FromUnixTimeMilliseconds(since).UtcDateTime
            : DateTime.MinValue;

        var items = await _db.ChatMessages
            .Where(m => m.ThreadUserId == threadUserId && m.CreatedAt > sinceDate)
            .OrderBy(m => m.CreatedAt)
            .Select(m => new
            {
                id = m.Id,
                body = m.Body,
                image = m.ImagePath,
                role = m.SenderRole,
                at = new DateTimeOffset(m.CreatedAt, TimeSpan.Zero).ToUnixTimeMilliseconds()
            })
            .ToListAsync();

        // Mark opposite-side as read
        var unreadIds = await _db.ChatMessages
            .Where(m => m.ThreadUserId == threadUserId && !m.IsRead && m.SenderRole != role)
            .ToListAsync();
        if (unreadIds.Any())
        {
            foreach (var m in unreadIds) m.IsRead = true;
            await _db.SaveChangesAsync();
        }

        return Json(items);
    }
}
