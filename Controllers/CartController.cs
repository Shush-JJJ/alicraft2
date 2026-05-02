using Alicraft2.Data;
using Alicraft2.Models;
using Alicraft2.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Alicraft2.Controllers;

[Authorize]
public class CartController : Controller
{
    private readonly AppDbContext _db;
    private readonly CurrentUser _current;
    private readonly IWebHostEnvironment _env;

    public CartController(AppDbContext db, CurrentUser current, IWebHostEnvironment env)
    {
        _db = db;
        _current = current;
        _env = env;
    }

    // Admins don't shop - send them to the admin dashboard if they hit a cart URL.
    public override void OnActionExecuting(Microsoft.AspNetCore.Mvc.Filters.ActionExecutingContext context)
    {
        if (User.IsInRole("Admin"))
        {
            context.Result = RedirectToAction("Index", "Admin");
            return;
        }
        base.OnActionExecuting(context);
    }

    public async Task<IActionResult> Index()
    {
        var uid = _current.Id!.Value;
        var items = await _db.CartItems
            .Include(c => c.Product)
            .Where(c => c.UserId == uid)
            .OrderBy(c => c.AddedAt)
            .ToListAsync();
        return View(items);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Add(int productId, int quantity = 1, string? customNote = null, IFormFile? customImage = null)
    {
        if (quantity < 1) quantity = 1;
        var product = await _db.Products.FirstOrDefaultAsync(p => p.Id == productId && p.IsActive);
        if (product == null) return NotFound();

        var uid = _current.Id!.Value;
        var existing = await _db.CartItems.FirstOrDefaultAsync(c => c.UserId == uid && c.ProductId == productId && c.CustomImagePath == null && string.IsNullOrEmpty(c.CustomNote));

        string? customImagePath = null;
        if (customImage != null && customImage.Length > 0)
            customImagePath = await FileHelper.SaveAsync(_env, customImage, "custom");

        if (existing != null && customImagePath == null && string.IsNullOrWhiteSpace(customNote))
        {
            existing.Quantity += quantity;
        }
        else
        {
            _db.CartItems.Add(new CartItem
            {
                UserId = uid,
                ProductId = productId,
                Quantity = quantity,
                CustomNote = string.IsNullOrWhiteSpace(customNote) ? null : customNote.Trim(),
                CustomImagePath = customImagePath
            });
        }
        await _db.SaveChangesAsync();

        TempData["Success"] = $"Added \"{product.Name}\" to your cart.";
        if (Request.Headers.Accept.ToString().Contains("application/json"))
            return Json(new { ok = true, count = await CountForAsync(uid) });
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateQty(int id, int quantity)
    {
        var uid = _current.Id!.Value;
        var item = await _db.CartItems.FirstOrDefaultAsync(c => c.Id == id && c.UserId == uid);
        if (item == null) return NotFound();
        if (quantity < 1) quantity = 1;
        item.Quantity = quantity;
        await _db.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Remove(int id)
    {
        var uid = _current.Id!.Value;
        var item = await _db.CartItems.FirstOrDefaultAsync(c => c.Id == id && c.UserId == uid);
        if (item == null) return NotFound();
        _db.CartItems.Remove(item);
        await _db.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    private async Task<int> CountForAsync(int uid)
        => await _db.CartItems.Where(c => c.UserId == uid).SumAsync(c => (int?)c.Quantity) ?? 0;
}
