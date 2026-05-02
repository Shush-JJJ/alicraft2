using System.Globalization;
using System.Text;
using Alicraft2.Data;
using Alicraft2.Models;
using Alicraft2.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Alicraft2.Controllers;

[Authorize(Policy = "AdminOnly")]
public class AdminController : Controller
{
    private readonly AppDbContext _db;
    private readonly IWebHostEnvironment _env;
    private readonly IEmailService _email;

    public AdminController(AppDbContext db, IWebHostEnvironment env, IEmailService email)
    {
        _db = db;
        _env = env;
        _email = email;
    }

    // ---------- EMAIL DIAGNOSTICS ----------
    [HttpGet]
    public IActionResult EmailDiag()
    {
        ViewBag.Diag = _email.GetDiagnostics();
        ViewBag.TestResult = TempData["TestResult"] as string;
        ViewBag.TestOk = TempData["TestOk"] as bool? ?? false;
        ViewBag.TestRecipient = TempData["TestRecipient"] as string;
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EmailDiagSend(string recipient)
    {
        if (string.IsNullOrWhiteSpace(recipient))
        {
            TempData["TestOk"] = false;
            TempData["TestResult"] = "Please enter a recipient email address.";
            return RedirectToAction(nameof(EmailDiag));
        }

        var subject = "AliCraft test email";
        var html = "<p>Hi! This is a test email sent from the AliCraft admin diagnostics page.</p>" +
                   $"<p>Time: {DateTime.UtcNow:u}</p>" +
                   "<p>If you received this, your live SMTP setup is working.</p>";
        var (ok, error) = await _email.TrySendAsync(recipient.Trim(), recipient.Trim(), subject, html);
        TempData["TestOk"] = ok && error == null;
        TempData["TestRecipient"] = recipient;
        TempData["TestResult"] = ok && error == null
            ? $"Test email sent to {recipient}. Check the inbox (and Spam)."
            : $"Send failed: {error}";
        return RedirectToAction(nameof(EmailDiag));
    }

    public async Task<IActionResult> Index()
    {
        ViewBag.TotalProducts = await _db.Products.CountAsync();
        ViewBag.ActiveProducts = await _db.Products.CountAsync(p => p.IsActive);
        ViewBag.TotalOrders = await _db.Orders.CountAsync();
        ViewBag.PendingOrders = await _db.Orders.CountAsync(o => o.Status == OrderStatus.Pending);
        ViewBag.ProcessingOrders = await _db.Orders.CountAsync(o => o.Status == OrderStatus.Processing);
        ViewBag.InTransitOrders = await _db.Orders.CountAsync(o => o.Status == OrderStatus.InTransit);
        ViewBag.DeliveredOrders = await _db.Orders.CountAsync(o => o.Status == OrderStatus.Delivered);
        ViewBag.TotalUsers = await _db.Users.CountAsync(u => u.Role == "User");
        ViewBag.TotalRevenue = await _db.Orders
            .Where(o => o.Status == OrderStatus.Delivered)
            .SumAsync(o => (decimal?)o.Total) ?? 0m;
        ViewBag.UnreadMessages = await _db.ChatMessages.CountAsync(m => m.SenderRole == "User" && !m.IsRead);
        ViewBag.RecentOrders = await _db.Orders
            .Include(o => o.User)
            .OrderByDescending(o => o.CreatedAt)
            .Take(8)
            .ToListAsync();
        return View();
    }

    // ---------- PRODUCTS CRUD ----------
    public async Task<IActionResult> Products(string? q)
    {
        IQueryable<Product> query = _db.Products;
        if (!string.IsNullOrWhiteSpace(q))
            query = query.Where(p => p.Name.ToLower().Contains(q.ToLower()));
        var list = await query.OrderByDescending(p => p.CreatedAt).ToListAsync();
        ViewBag.Query = q ?? "";
        return View(list);
    }

    [HttpGet] public IActionResult CreateProduct() => View(new ProductFormVm());

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateProduct(ProductFormVm vm)
    {
        if (!ModelState.IsValid) return View(vm);
        string? img = null;
        if (vm.Image != null && vm.Image.Length > 0)
            img = await FileHelper.SaveAsync(_env, vm.Image, "products");

        _db.Products.Add(new Product
        {
            Name = vm.Name.Trim(),
            Description = vm.Description.Trim(),
            Category = vm.Category,
            Price = vm.Price,
            Stock = vm.Stock,
            IsActive = vm.IsActive,
            ImagePath = img ?? "/images/placeholder-frame.svg"
        });
        await _db.SaveChangesAsync();
        TempData["Success"] = "Product created.";
        return RedirectToAction(nameof(Products));
    }

    [HttpGet]
    public async Task<IActionResult> EditProduct(int id)
    {
        var p = await _db.Products.FindAsync(id);
        if (p == null) return NotFound();
        return View(new ProductFormVm
        {
            Id = p.Id,
            Name = p.Name,
            Description = p.Description,
            Category = p.Category,
            Price = p.Price,
            Stock = p.Stock,
            IsActive = p.IsActive,
            ExistingImagePath = p.ImagePath
        });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> EditProduct(ProductFormVm vm)
    {
        var p = await _db.Products.FindAsync(vm.Id);
        if (p == null) return NotFound();
        if (!ModelState.IsValid)
        {
            vm.ExistingImagePath = p.ImagePath;
            return View(vm);
        }
        p.Name = vm.Name.Trim();
        p.Description = vm.Description.Trim();
        p.Category = vm.Category;
        p.Price = vm.Price;
        p.Stock = vm.Stock;
        p.IsActive = vm.IsActive;
        if (vm.Image != null && vm.Image.Length > 0)
        {
            var img = await FileHelper.SaveAsync(_env, vm.Image, "products");
            if (img != null) p.ImagePath = img;
        }
        await _db.SaveChangesAsync();
        TempData["Success"] = "Product updated.";
        return RedirectToAction(nameof(Products));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteProduct(int id)
    {
        var p = await _db.Products.FindAsync(id);
        if (p == null) return NotFound();
        // Soft delete if referenced by orders, else hard delete
        var used = await _db.OrderItems.AnyAsync(i => i.ProductId == id);
        if (used)
        {
            p.IsActive = false;
            TempData["Success"] = "Product archived (had past orders).";
        }
        else
        {
            _db.Products.Remove(p);
            TempData["Success"] = "Product deleted.";
        }
        await _db.SaveChangesAsync();
        return RedirectToAction(nameof(Products));
    }

    // ---------- ORDERS ----------
    public async Task<IActionResult> Orders(string? status, string? q)
    {
        IQueryable<Order> query = _db.Orders.Include(o => o.User).Include(o => o.Items);
        if (!string.IsNullOrWhiteSpace(status) && status != "All")
            query = query.Where(o => o.Status == status);
        if (!string.IsNullOrWhiteSpace(q))
        {
            var s = q.ToLower();
            query = query.Where(o => o.OrderNumber.ToLower().Contains(s) || (o.User != null && o.User.Email.ToLower().Contains(s)));
        }
        var orders = await query.OrderByDescending(o => o.CreatedAt).ToListAsync();
        ViewBag.Status = status ?? "All";
        ViewBag.Query = q ?? "";
        return View(orders);
    }

    public async Task<IActionResult> OrderDetails(int id)
    {
        var order = await _db.Orders
            .Include(o => o.User)
            .Include(o => o.Items).ThenInclude(i => i.Product)
            .FirstOrDefaultAsync(o => o.Id == id);
        if (order == null) return NotFound();
        return View(order);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateOrderStatus(int id, string status)
    {
        var order = await _db.Orders.FindAsync(id);
        if (order == null) return NotFound();

        switch (status)
        {
            case OrderStatus.Pending:
                order.Status = OrderStatus.Pending;
                break;
            case OrderStatus.Processing:
                order.Status = OrderStatus.Processing;
                order.ProcessingAt = DateTime.UtcNow;
                break;
            case OrderStatus.InTransit:
                order.Status = OrderStatus.InTransit;
                order.InTransitAt = DateTime.UtcNow;
                break;
            case OrderStatus.Delivered:
                order.Status = OrderStatus.Delivered;
                order.DeliveredAt = DateTime.UtcNow;
                break;
            case OrderStatus.Cancelled:
                order.Status = OrderStatus.Cancelled;
                order.CancelledAt = DateTime.UtcNow;
                break;
            default:
                TempData["Error"] = "Unknown status.";
                return RedirectToAction(nameof(OrderDetails), new { id });
        }

        await _db.SaveChangesAsync();
        TempData["Success"] = $"Order status updated to {status}.";
        return RedirectToAction(nameof(OrderDetails), new { id });
    }

    // ---------- CHAT INBOX ----------
    public async Task<IActionResult> Inbox()
    {
        var threads = await _db.ChatMessages
            .GroupBy(m => m.ThreadUserId)
            .Select(g => new
            {
                UserId = g.Key,
                LastAt = g.Max(x => x.CreatedAt),
                Unread = g.Count(x => x.SenderRole == "User" && !x.IsRead),
                Last = g.OrderByDescending(x => x.CreatedAt).First().Body
            })
            .ToListAsync();

        var userIds = threads.Select(t => t.UserId).ToList();
        var users = await _db.Users.Where(u => userIds.Contains(u.Id)).ToListAsync();

        var vm = threads
            .Select(t => new
            {
                t.UserId,
                User = users.FirstOrDefault(u => u.Id == t.UserId),
                t.LastAt,
                t.Unread,
                t.Last
            })
            .OrderByDescending(x => x.LastAt)
            .ToList();
        return View(vm);
    }

    // ---------- USERS ----------
    public async Task<IActionResult> Users()
    {
        var users = await _db.Users.Where(u => u.Role != "Admin").OrderByDescending(u => u.CreatedAt).ToListAsync();
        return View(users);
    }

    // ---------- REPORTS ----------
    public async Task<IActionResult> Reports(DateTime? from, DateTime? to, string? preset)
    {
        var range = ResolveRange(from, to, preset);
        from = range.from; to = range.to;

        IQueryable<Order> q = _db.Orders.Include(o => o.Items).Include(o => o.User);
        if (range.fromUtc.HasValue) q = q.Where(o => o.CreatedAt >= range.fromUtc);
        if (range.toUtc.HasValue)   q = q.Where(o => o.CreatedAt <= range.toUtc);

        var allOrders = await q.ToListAsync();
        var todayUtc = DateTime.UtcNow.Date;
        var delivered = allOrders.Where(o => o.Status == OrderStatus.Delivered).ToList();

        ViewBag.From = from;
        ViewBag.To = to;
        ViewBag.Preset = preset ?? "";
        ViewBag.RangeLabel = (from.HasValue && to.HasValue)
            ? $"{from.Value:MMM d, yyyy} – {to.Value:MMM d, yyyy}"
            : "All time";

        ViewBag.TotalRevenue = delivered.Sum(o => o.Total);
        ViewBag.TotalOrders = allOrders.Count;
        ViewBag.DeliveredOrders = delivered.Count;
        ViewBag.AvgOrderValue = delivered.Any() ? delivered.Average(o => o.Total) : 0m;
        ViewBag.TotalUnitsSold = delivered.Sum(o => o.Items.Sum(i => i.Quantity));

        // Status breakdown
        ViewBag.StatusBreakdown = new[]
        {
            new { Status = OrderStatus.Pending,    Count = allOrders.Count(o => o.Status == OrderStatus.Pending) },
            new { Status = OrderStatus.Processing, Count = allOrders.Count(o => o.Status == OrderStatus.Processing) },
            new { Status = OrderStatus.InTransit,  Count = allOrders.Count(o => o.Status == OrderStatus.InTransit) },
            new { Status = OrderStatus.Delivered,  Count = allOrders.Count(o => o.Status == OrderStatus.Delivered) },
            new { Status = OrderStatus.Cancelled,  Count = allOrders.Count(o => o.Status == OrderStatus.Cancelled) }
        };

        // Revenue by day across the selected range (capped to 90 buckets so the chart stays readable)
        var rangeStart = from?.Date ?? (allOrders.Any() ? allOrders.Min(o => o.CreatedAt).Date : todayUtc.AddDays(-13));
        var rangeEnd   = to?.Date   ?? (allOrders.Any() ? allOrders.Max(o => o.CreatedAt).Date : todayUtc);
        var totalDays = (rangeEnd - rangeStart).Days + 1;
        if (totalDays < 1) totalDays = 1;

        if (totalDays <= 90)
        {
            var days = Enumerable.Range(0, totalDays).Select(i => rangeStart.AddDays(i)).ToList();
            ViewBag.RevenueByDay = days
                .Select(d => new
                {
                    Date = d,
                    Revenue = delivered
                        .Where(o => (o.DeliveredAt ?? o.CreatedAt).Date == d)
                        .Sum(o => o.Total)
                })
                .ToArray();
            ViewBag.RevenueBucket = "day";
        }
        else
        {
            // Aggregate by week to keep the chart readable for long ranges
            var weeks = new List<DateTime>();
            for (var d = rangeStart; d <= rangeEnd; d = d.AddDays(7)) weeks.Add(d);
            ViewBag.RevenueByDay = weeks
                .Select(w => new
                {
                    Date = w,
                    Revenue = delivered
                        .Where(o => { var od = (o.DeliveredAt ?? o.CreatedAt).Date; return od >= w && od < w.AddDays(7); })
                        .Sum(o => o.Total)
                })
                .ToArray();
            ViewBag.RevenueBucket = "week";
        }

        // Top products (orders in range)
        var itemsFlat = allOrders.SelectMany(o => o.Items).ToList();
        ViewBag.TopProducts = itemsFlat
            .GroupBy(i => new { i.ProductId, i.ProductName })
            .Select(g => new
            {
                g.Key.ProductName,
                Units = g.Sum(x => x.Quantity),
                Revenue = g.Sum(x => x.UnitPrice * x.Quantity)
            })
            .OrderByDescending(x => x.Units)
            .Take(5)
            .ToArray();

        // Payment mix (delivered, in range)
        ViewBag.PaymentMix = delivered
            .GroupBy(o => o.PaymentMethod)
            .Select(g => new { Method = g.Key, Count = g.Count(), Revenue = g.Sum(o => o.Total) })
            .OrderByDescending(x => x.Revenue)
            .ToArray();

        // Top customers (by delivered spend, in range)
        ViewBag.TopCustomers = delivered
            .GroupBy(o => new { o.UserId, Name = o.User?.Name ?? "(deleted)", Email = o.User?.Email ?? "" })
            .Select(g => new
            {
                g.Key.Name, g.Key.Email,
                Orders = g.Count(),
                Revenue = g.Sum(o => o.Total)
            })
            .OrderByDescending(x => x.Revenue)
            .Take(5)
            .ToArray();

        return View();
    }

    // ---------- CSV EXPORT (one row per order) ----------
    [HttpGet]
    public async Task<IActionResult> ExportOrders(DateTime? from, DateTime? to, string? preset)
    {
        var range = ResolveRange(from, to, preset);

        IQueryable<Order> q = _db.Orders.Include(o => o.Items).Include(o => o.User);
        if (range.fromUtc.HasValue) q = q.Where(o => o.CreatedAt >= range.fromUtc);
        if (range.toUtc.HasValue)   q = q.Where(o => o.CreatedAt <= range.toUtc);

        var orders = await q.OrderBy(o => o.CreatedAt).ToListAsync();

        var sb = new StringBuilder();
        sb.AppendLine("Order Number,Created At (UTC),Customer Name,Customer Email,Status,Payment Method,Payment Reference,Items,Units,Subtotal,Shipping,Total,Province,City,Barangay,Notes");
        var inv = CultureInfo.InvariantCulture;
        foreach (var o in orders)
        {
            var units = o.Items.Sum(i => i.Quantity);
            var itemDesc = string.Join(" | ", o.Items.Select(i => $"{i.Quantity}x {i.ProductName}"));
            sb.AppendLine(string.Join(",", new[]
            {
                Csv(o.OrderNumber),
                Csv(o.CreatedAt.ToString("yyyy-MM-dd HH:mm", inv)),
                Csv(o.User?.Name ?? "(deleted)"),
                Csv(o.User?.Email ?? ""),
                Csv(o.Status),
                Csv(o.PaymentMethod),
                Csv(o.PaymentReference ?? ""),
                Csv(itemDesc),
                units.ToString(inv),
                o.Subtotal.ToString("0.00", inv),
                o.Shipping.ToString("0.00", inv),
                o.Total.ToString("0.00", inv),
                Csv(o.ShippingProvince),
                Csv(o.ShippingCity),
                Csv(o.ShippingBarangay),
                Csv(o.Notes ?? "")
            }));
        }

        var rangeLabel = (range.from.HasValue && range.to.HasValue)
            ? $"{range.from.Value:yyyy-MM-dd}_to_{range.to.Value:yyyy-MM-dd}"
            : "all-time";

        // UTF-8 BOM so Excel auto-detects encoding (important for accented names).
        var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(sb.ToString())).ToArray();
        return File(bytes, "text/csv; charset=utf-8", $"alicraft-orders-{rangeLabel}.csv");
    }

    // ---------- helpers ----------
    private static (DateTime? from, DateTime? to, DateTime? fromUtc, DateTime? toUtc, string preset)
        ResolveRange(DateTime? from, DateTime? to, string? preset)
    {
        var todayUtc = DateTime.UtcNow.Date;
        switch ((preset ?? "").ToLowerInvariant())
        {
            case "today":  from = todayUtc;              to = todayUtc; break;
            case "7d":     from = todayUtc.AddDays(-6);  to = todayUtc; break;
            case "30d":    from = todayUtc.AddDays(-29); to = todayUtc; break;
            case "90d":    from = todayUtc.AddDays(-89); to = todayUtc; break;
            case "ytd":    from = new DateTime(todayUtc.Year, 1, 1); to = todayUtc; break;
            case "all":    from = null; to = null; break;
        }
        if (from == null && to == null && string.IsNullOrEmpty(preset))
        {
            from = todayUtc.AddDays(-29);
            to = todayUtc;
        }
        if (from.HasValue && to.HasValue && from.Value.Date > to.Value.Date)
            (from, to) = (to, from);
        DateTime? fromUtc = from.HasValue ? DateTime.SpecifyKind(from.Value.Date, DateTimeKind.Utc) : null;
        DateTime? toUtc   = to.HasValue   ? DateTime.SpecifyKind(to.Value.Date.AddDays(1).AddTicks(-1), DateTimeKind.Utc) : null;
        return (from, to, fromUtc, toUtc, preset ?? "");
    }

    private static string Csv(string? s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        if (s.IndexOfAny(new[] { ',', '"', '\n', '\r' }) >= 0)
            return "\"" + s.Replace("\"", "\"\"") + "\"";
        return s;
    }
}
