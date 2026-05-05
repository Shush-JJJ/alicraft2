using Alicraft2.Data;
using Alicraft2.Models;
using Alicraft2.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Alicraft2.Controllers;

[Authorize]
public class OrdersController : Controller
{
    private readonly AppDbContext _db;
    private readonly CurrentUser _current;
    private readonly IWebHostEnvironment _env;

    public OrdersController(AppDbContext db, CurrentUser current, IWebHostEnvironment env)
    {
        _db = db;
        _current = current;
        _env = env;
    }

    // Admins don't have a customer order history - send them to the admin orders page instead.
    public override void OnActionExecuting(Microsoft.AspNetCore.Mvc.Filters.ActionExecutingContext context)
    {
        if (User.IsInRole("Admin"))
        {
            context.Result = RedirectToAction("Orders", "Admin");
            return;
        }
        base.OnActionExecuting(context);
    }

    // GET /Orders  -> list user orders
    public async Task<IActionResult> Index(string? status)
    {
        var uid = _current.Id!.Value;
        IQueryable<Order> q = _db.Orders.Include(o => o.Items).Where(o => o.UserId == uid);
        if (!string.IsNullOrWhiteSpace(status) && status != "All")
            q = q.Where(o => o.Status == status);
        var orders = await q.OrderByDescending(o => o.CreatedAt).ToListAsync();
        ViewBag.Status = status ?? "All";
        return View(orders);
    }

    public async Task<IActionResult> Details(int id)
    {
        var uid = _current.Id!.Value;
        var order = await _db.Orders
            .Include(o => o.Items).ThenInclude(i => i.Product)
            .FirstOrDefaultAsync(o => o.Id == id && o.UserId == uid);
        if (order == null) return NotFound();
        return View(order);
    }

    public async Task<IActionResult> Track(int id)
    {
        var uid = _current.Id!.Value;
        var order = await _db.Orders
            .Include(o => o.Items).ThenInclude(i => i.Product)
            .FirstOrDefaultAsync(o => o.Id == id && o.UserId == uid);
        if (order == null) return NotFound();
        return View(order);
    }

    // ---------- CHECKOUT ----------
    [HttpGet]
    public async Task<IActionResult> Checkout()
    {
        var uid = _current.Id!.Value;
        var cart = await _db.CartItems.Include(c => c.Product).Where(c => c.UserId == uid).ToListAsync();
        if (cart.Count == 0)
        {
            TempData["Error"] = "Your cart is empty.";
            return RedirectToAction("Index", "Cart");
        }
        var user = await _current.GetAsync();
        var initialFee = ShippingZones.FeeFor(user?.Province) ?? ShippingZones.DefaultFee;
        ViewBag.Cart = cart;
        ViewBag.Subtotal = cart.Sum(c => c.Quantity * (c.Product?.Price ?? 0));
        ViewBag.Shipping = initialFee;
        ViewBag.ShippingZone = ShippingZones.LabelFor(user?.Province);
        var vm = new CheckoutVm
        {
            ShippingName = user?.Name ?? "",
            ShippingPhone = user?.Phone ?? "",
            ShippingProvince = user?.Province ?? "",
            ShippingCity = user?.City ?? "",
            ShippingBarangay = user?.Barangay ?? "",
            ShippingStreet = user?.Street ?? "",
            ShippingPostalCode = user?.PostalCode ?? "",
            PaymentMethod = "GCash"
        };
        return View(vm);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Checkout(CheckoutVm vm)
    {
        var uid = _current.Id!.Value;
        var cart = await _db.CartItems.Include(c => c.Product).Where(c => c.UserId == uid).ToListAsync();
        if (cart.Count == 0)
        {
            TempData["Error"] = "Your cart is empty.";
            return RedirectToAction("Index", "Cart");
        }

        // Local helper to repopulate the ViewBag bits the form needs when re-rendering.
        decimal RebuildViewBag()
        {
            var fee = ShippingZones.FeeFor(vm.ShippingProvince) ?? ShippingZones.DefaultFee;
            ViewBag.Cart = cart;
            ViewBag.Subtotal = cart.Sum(c => c.Quantity * (c.Product?.Price ?? 0));
            ViewBag.Shipping = fee;
            ViewBag.ShippingZone = ShippingZones.LabelFor(vm.ShippingProvince);
            return fee;
        }

        if (!ModelState.IsValid)
        {
            RebuildViewBag();
            return View(vm);
        }

        // Block deliveries outside Luzon — the store can only ship within Luzon.
        if (!ShippingZones.IsServiceable(vm.ShippingProvince))
        {
            ModelState.AddModelError(nameof(vm.ShippingProvince),
                "Sorry, we currently only deliver within Luzon. Please choose a Luzon province.");
            RebuildViewBag();
            return View(vm);
        }

        // Validate payment method and require reference/proof for digital methods
        var validMethods = new[] { "GCash", "PayMaya", "COD" };
        if (!validMethods.Contains(vm.PaymentMethod))
        {
            ModelState.AddModelError(nameof(vm.PaymentMethod), "Please choose a valid payment method.");
            RebuildViewBag();
            return View(vm);
        }

        if ((vm.PaymentMethod == "GCash" || vm.PaymentMethod == "PayMaya") &&
            string.IsNullOrWhiteSpace(vm.PaymentReference) &&
            (vm.PaymentProof == null || vm.PaymentProof.Length == 0))
        {
            ModelState.AddModelError(nameof(vm.PaymentReference),
                $"Please enter your {vm.PaymentMethod} reference number or upload a payment screenshot.");
            RebuildViewBag();
            return View(vm);
        }

        string? proof = null;
        if (vm.PaymentProof != null && vm.PaymentProof.Length > 0)
            proof = await FileHelper.SaveAsync(_env, vm.PaymentProof, "payments");

        var subtotal = cart.Sum(c => c.Quantity * (c.Product?.Price ?? 0));
        // Authoritative server-side fee — never trust whatever number the form posted.
        var shipping = ShippingZones.FeeFor(vm.ShippingProvince)!.Value;
        var order = new Order
        {
            UserId = uid,
            OrderNumber = $"AC-{DateTime.UtcNow:yyyyMMdd}-{Random.Shared.Next(1000, 9999)}",
            Subtotal = subtotal,
            Shipping = shipping,
            Total = subtotal + shipping,
            Status = OrderStatus.Pending,
            PaymentMethod = vm.PaymentMethod,
            PaymentReference = string.IsNullOrWhiteSpace(vm.PaymentReference) ? null : vm.PaymentReference.Trim(),
            PaymentProofPath = proof,
            ShippingName = vm.ShippingName.Trim(),
            ShippingPhone = vm.ShippingPhone.Trim(),
            ShippingProvince = vm.ShippingProvince,
            ShippingCity = vm.ShippingCity,
            ShippingBarangay = vm.ShippingBarangay,
            ShippingStreet = vm.ShippingStreet,
            ShippingPostalCode = vm.ShippingPostalCode,
            Notes = string.IsNullOrWhiteSpace(vm.Notes) ? null : vm.Notes.Trim()
        };

        foreach (var c in cart)
        {
            order.Items.Add(new OrderItem
            {
                ProductId = c.ProductId,
                ProductName = c.Product?.Name ?? "Item",
                UnitPrice = c.Product?.Price ?? 0,
                Quantity = c.Quantity,
                CustomNote = c.CustomNote,
                CustomImagePath = c.CustomImagePath
            });

            if (c.Product != null)
                c.Product.Stock = Math.Max(0, c.Product.Stock - c.Quantity);
        }

        _db.Orders.Add(order);
        _db.CartItems.RemoveRange(cart);
        await _db.SaveChangesAsync();

        TempData["Success"] = $"Order {order.OrderNumber} placed! We will process it shortly.";
        return RedirectToAction(nameof(Track), new { id = order.Id });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Reorder(int id)
    {
        var uid = _current.Id!.Value;
        var order = await _db.Orders
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.Id == id && o.UserId == uid);
        if (order == null) return NotFound();

        int added = 0, skipped = 0;
        foreach (var item in order.Items)
        {
            var product = await _db.Products.FirstOrDefaultAsync(p => p.Id == item.ProductId && p.IsActive);
            if (product == null || product.Stock <= 0) { skipped++; continue; }

            var qty = Math.Min(item.Quantity, product.Stock);
            var existing = await _db.CartItems.FirstOrDefaultAsync(c =>
                c.UserId == uid && c.ProductId == item.ProductId && c.CustomImagePath == null && string.IsNullOrEmpty(c.CustomNote));
            if (existing != null)
            {
                existing.Quantity += qty;
            }
            else
            {
                _db.CartItems.Add(new CartItem
                {
                    UserId = uid,
                    ProductId = item.ProductId,
                    Quantity = qty
                });
            }
            added++;
        }
        await _db.SaveChangesAsync();

        TempData["Success"] = skipped > 0
            ? $"Added {added} item(s) to your cart. {skipped} item(s) were skipped (unavailable)."
            : $"Added {added} item(s) from order {order.OrderNumber} back to your cart.";
        return RedirectToAction("Index", "Cart");
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Cancel(int id)
    {
        var uid = _current.Id!.Value;
        var order = await _db.Orders.FirstOrDefaultAsync(o => o.Id == id && o.UserId == uid);
        if (order == null) return NotFound();
        if (order.Status == OrderStatus.Pending || order.Status == OrderStatus.Processing)
        {
            order.Status = OrderStatus.Cancelled;
            order.CancelledAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
            TempData["Success"] = "Order cancelled.";
        }
        else
        {
            TempData["Error"] = "This order can no longer be cancelled.";
        }
        return RedirectToAction(nameof(Details), new { id });
    }
}
