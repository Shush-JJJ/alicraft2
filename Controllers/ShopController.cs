using Alicraft2.Data;
using Alicraft2.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Alicraft2.Controllers;

public class ShopController : Controller
{
    private readonly AppDbContext _db;
    public ShopController(AppDbContext db) => _db = db;

    public async Task<IActionResult> Index(string? category, string? q, string? sort)
    {
        IQueryable<Product> query = _db.Products.Where(p => p.IsActive);
        if (!string.IsNullOrWhiteSpace(category))
            query = query.Where(p => p.Category == category);
        if (!string.IsNullOrWhiteSpace(q))
        {
            var s = q.Trim().ToLower();
            query = query.Where(p => p.Name.ToLower().Contains(s) || p.Description.ToLower().Contains(s));
        }

        query = sort switch
        {
            "price-asc"  => query.OrderBy(p => p.Price),
            "price-desc" => query.OrderByDescending(p => p.Price),
            "name"       => query.OrderBy(p => p.Name),
            _            => query.OrderByDescending(p => p.CreatedAt)
        };

        var products = await query.ToListAsync();
        ViewBag.Categories = await _db.Products
            .Where(p => p.IsActive && p.Category != null && p.Category != "")
            .Select(p => p.Category!)
            .Distinct()
            .OrderBy(c => c)
            .ToListAsync();
        ViewBag.Category = category ?? "All";
        ViewBag.Query = q ?? "";
        ViewBag.Sort = sort ?? "newest";
        return View(products);
    }

    public async Task<IActionResult> Details(int id)
    {
        var product = await _db.Products.FirstOrDefaultAsync(p => p.Id == id && p.IsActive);
        if (product == null) return NotFound();
        return View(product);
    }
}
