using Alicraft2.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;

namespace Alicraft2.Data;

public static class DbInitializer
{
    // Catalog of products the shop ships with. The image files live in
    // wwwroot/images/ so they are baked into the Docker image and survive
    // every Render redeploy. Order matters: the orders-seed below references
    // these by index (0..5).
    private static readonly (string Name, string Description, string Category, decimal Price, int Stock, string ImagePath)[] SeedProducts = new[]
    {
        ("Rectangle Lithophane",
         "A wide rectangular lithophane plaque that reveals your photo's depth when lit from behind. Perfect for mantels, desks, and living-room displays. Send us the photo — we handle the rest.",
         "Frame", 799m, 25, "/images/rectangle-lithophane.webp"),
        ("Square Lithophane",
         "A classic square lithophane tile for portrait photos. Compact and versatile — fits any shelf, nightstand, or wall display.",
         "Frame", 699m, 30, "/images/square-lithophane.webp"),
        ("3D Box Lithophane",
         "A four-sided cube lithophane showcasing four of your favorite photos, one per face. A glowing centerpiece that rotates your most cherished memories.",
         "3D Display", 1_499m, 11, "/images/3d-box-lithophane.webp"),
        ("Rectangle Keychain Lithophane",
         "Carry your favorite memory in your pocket. Rectangular photo keychain that reveals its image in stunning 3D when held to the light. Sturdy metal ring included.",
         "Keychain", 199m, 97, "/images/rectangle-keychain-lithophane.webp"),
        ("Square Keychain Lithophane",
         "A compact square photo keychain — perfect for portraits and pet faces. Lightweight, durable, and ready to go everywhere you do.",
         "Keychain", 249m, 80, "/images/square-keychain-lithophane.jpg"),
        ("Heart Keychain Lithophane",
         "A romantic heart-shaped keychain for couples, best friends, and family. Holds your photo in timeless 3D detail — the perfect little gift.",
         "Keychain", 299m, 30, "/images/heart-keychain-lithophane.webp"),
    };

    public static async Task SeedAsync(AppDbContext db)
    {
        await db.Database.EnsureCreatedAsync();

        // EnsureCreatedAsync skips schema creation if the database already has
        // ANY tables. Managed Postgres providers (Neon especially) ship the
        // default `neondb` database with extension/system objects preinstalled,
        // which tricks EF Core into thinking our schema is already there.
        // Explicitly verify our Users table exists; if it doesn't, force the
        // full schema to be created.
        if (!await OurSchemaExistsAsync(db))
        {
            var creator = (RelationalDatabaseCreator)db.Database.GetService<IDatabaseCreator>();
            await creator.CreateTablesAsync();
        }

        await EnsureSchemaUpgradesAsync(db);

        if (!await db.Users.AnyAsync())
        {
            db.Users.Add(new User
            {
                Name = "AliCraft Admin",
                Email = "admin@alicraft.com",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("Admin123!"),
                Phone = "09171234567",
                Role = "Admin",
                Province = "Metro Manila",
                City = "Quezon City",
                Barangay = "Diliman",
                Street = "Admin HQ",
                PostalCode = "1101",
                SecurityQuestion = "What is our shop name?",
                SecurityAnswerHash = BCrypt.Net.BCrypt.HashPassword("alicraft"),
                IsEmailVerified = true
            });
            db.Users.Add(new User
            {
                Name = "Demo Customer",
                Email = "demo@alicraft.com",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("Demo123!"),
                Phone = "09179876543",
                Role = "User",
                Province = "Metro Manila",
                City = "Manila",
                Barangay = "Ermita",
                Street = "123 Rizal St.",
                PostalCode = "1000",
                SecurityQuestion = "What is our shop name?",
                SecurityAnswerHash = BCrypt.Net.BCrypt.HashPassword("alicraft"),
                IsEmailVerified = true
            });
            await db.SaveChangesAsync();
        }

        // Fresh seed OR migrate-in-place if a previous deploy seeded the old placeholder set.
        // Detection: any product whose ImagePath points at a "/images/placeholder-" SVG.
        // Matched by ordinal so existing FK references (orders, cart items) stay intact.
        var products = await db.Products.OrderBy(p => p.Id).ToListAsync();
        var hasPlaceholders = products.Any(p => p.ImagePath != null && p.ImagePath.Contains("placeholder-"));

        if (products.Count == 0)
        {
            foreach (var s in SeedProducts)
            {
                db.Products.Add(new Product
                {
                    Name = s.Name, Description = s.Description, Category = s.Category,
                    Price = s.Price, Stock = s.Stock, ImagePath = s.ImagePath, IsActive = true
                });
            }
            await db.SaveChangesAsync();
        }
        else if (hasPlaceholders)
        {
            // Upgrade each old placeholder row to the real product (by ordinal position).
            for (int i = 0; i < products.Count && i < SeedProducts.Length; i++)
            {
                var s = SeedProducts[i];
                products[i].Name        = s.Name;
                products[i].Description = s.Description;
                products[i].Category    = s.Category;
                products[i].Price       = s.Price;
                products[i].Stock       = s.Stock;
                products[i].ImagePath   = s.ImagePath;
                products[i].IsActive    = true;
            }
            // If the migration adds NEW products beyond the count we already had, append them.
            for (int i = products.Count; i < SeedProducts.Length; i++)
            {
                var s = SeedProducts[i];
                db.Products.Add(new Product
                {
                    Name = s.Name, Description = s.Description, Category = s.Category,
                    Price = s.Price, Stock = s.Stock, ImagePath = s.ImagePath, IsActive = true
                });
            }
            await db.SaveChangesAsync();
        }

        if (!await db.Orders.AnyAsync())
        {
            var demo = await db.Users.FirstOrDefaultAsync(u => u.Email == "demo@alicraft.com");
            var orderProducts = await db.Products.OrderBy(p => p.Id).ToListAsync();
            if (demo != null && orderProducts.Count >= 6)
            {
                var now = DateTime.UtcNow;

                // Delivered order 12 days ago
                var o1 = new Order
                {
                    UserId = demo.Id,
                    OrderNumber = $"AC-{now.AddDays(-12):yyyyMMdd}-1001",
                    Status = OrderStatus.Delivered,
                    PaymentMethod = "GCash",
                    PaymentReference = "G-DEMO-0001",
                    ShippingName = demo.Name, ShippingPhone = demo.Phone,
                    ShippingProvince = demo.Province!, ShippingCity = demo.City!, ShippingBarangay = demo.Barangay!,
                    ShippingStreet = demo.Street!, ShippingPostalCode = demo.PostalCode!,
                    CreatedAt = now.AddDays(-12),
                    ProcessingAt = now.AddDays(-12).AddHours(4),
                    InTransitAt = now.AddDays(-11),
                    DeliveredAt = now.AddDays(-10)
                };
                o1.Items.Add(new OrderItem { ProductId = orderProducts[0].Id, ProductName = orderProducts[0].Name, UnitPrice = orderProducts[0].Price, Quantity = 1 });
                o1.Items.Add(new OrderItem { ProductId = orderProducts[3].Id, ProductName = orderProducts[3].Name, UnitPrice = orderProducts[3].Price, Quantity = 2 });
                o1.Subtotal = o1.Items.Sum(i => i.UnitPrice * i.Quantity);
                o1.Shipping = 79m;
                o1.Total = o1.Subtotal + o1.Shipping;

                // Delivered order 5 days ago
                var o2 = new Order
                {
                    UserId = demo.Id,
                    OrderNumber = $"AC-{now.AddDays(-5):yyyyMMdd}-1002",
                    Status = OrderStatus.Delivered,
                    PaymentMethod = "COD",
                    ShippingName = demo.Name, ShippingPhone = demo.Phone,
                    ShippingProvince = demo.Province!, ShippingCity = demo.City!, ShippingBarangay = demo.Barangay!,
                    ShippingStreet = demo.Street!, ShippingPostalCode = demo.PostalCode!,
                    CreatedAt = now.AddDays(-5),
                    ProcessingAt = now.AddDays(-5).AddHours(6),
                    InTransitAt = now.AddDays(-4),
                    DeliveredAt = now.AddDays(-3)
                };
                o2.Items.Add(new OrderItem { ProductId = orderProducts[1].Id, ProductName = orderProducts[1].Name, UnitPrice = orderProducts[1].Price, Quantity = 1 });
                o2.Items.Add(new OrderItem { ProductId = orderProducts[4].Id, ProductName = orderProducts[4].Name, UnitPrice = orderProducts[4].Price, Quantity = 1 });
                o2.Subtotal = o2.Items.Sum(i => i.UnitPrice * i.Quantity);
                o2.Shipping = 79m;
                o2.Total = o2.Subtotal + o2.Shipping;

                // In-transit order yesterday
                var o3 = new Order
                {
                    UserId = demo.Id,
                    OrderNumber = $"AC-{now.AddDays(-1):yyyyMMdd}-1003",
                    Status = OrderStatus.InTransit,
                    PaymentMethod = "GCash",
                    PaymentReference = "G-DEMO-0003",
                    ShippingName = demo.Name, ShippingPhone = demo.Phone,
                    ShippingProvince = demo.Province!, ShippingCity = demo.City!, ShippingBarangay = demo.Barangay!,
                    ShippingStreet = demo.Street!, ShippingPostalCode = demo.PostalCode!,
                    CreatedAt = now.AddDays(-1),
                    ProcessingAt = now.AddDays(-1).AddHours(2),
                    InTransitAt = now.AddHours(-6)
                };
                o3.Items.Add(new OrderItem { ProductId = orderProducts[2].Id, ProductName = orderProducts[2].Name, UnitPrice = orderProducts[2].Price, Quantity = 1 });
                o3.Subtotal = o3.Items.Sum(i => i.UnitPrice * i.Quantity);
                o3.Shipping = 79m;
                o3.Total = o3.Subtotal + o3.Shipping;

                // Processing order a few hours ago
                var o4 = new Order
                {
                    UserId = demo.Id,
                    OrderNumber = $"AC-{now:yyyyMMdd}-1004",
                    Status = OrderStatus.Processing,
                    PaymentMethod = "GCash",
                    PaymentReference = "G-DEMO-0004",
                    ShippingName = demo.Name, ShippingPhone = demo.Phone,
                    ShippingProvince = demo.Province!, ShippingCity = demo.City!, ShippingBarangay = demo.Barangay!,
                    ShippingStreet = demo.Street!, ShippingPostalCode = demo.PostalCode!,
                    CreatedAt = now.AddHours(-5),
                    ProcessingAt = now.AddHours(-4)
                };
                o4.Items.Add(new OrderItem { ProductId = orderProducts[5].Id, ProductName = orderProducts[5].Name, UnitPrice = orderProducts[5].Price, Quantity = 2, CustomNote = "Please print with my dog Luna." });
                o4.Subtotal = o4.Items.Sum(i => i.UnitPrice * i.Quantity);
                o4.Shipping = 79m;
                o4.Total = o4.Subtotal + o4.Shipping;

                // Pending order just now
                var o5 = new Order
                {
                    UserId = demo.Id,
                    OrderNumber = $"AC-{now:yyyyMMdd}-1005",
                    Status = OrderStatus.Pending,
                    PaymentMethod = "COD",
                    ShippingName = demo.Name, ShippingPhone = demo.Phone,
                    ShippingProvince = demo.Province!, ShippingCity = demo.City!, ShippingBarangay = demo.Barangay!,
                    ShippingStreet = demo.Street!, ShippingPostalCode = demo.PostalCode!,
                    CreatedAt = now.AddMinutes(-30)
                };
                o5.Items.Add(new OrderItem { ProductId = orderProducts[3].Id, ProductName = orderProducts[3].Name, UnitPrice = orderProducts[3].Price, Quantity = 3 });
                o5.Subtotal = o5.Items.Sum(i => i.UnitPrice * i.Quantity);
                o5.Shipping = 79m;
                o5.Total = o5.Subtotal + o5.Shipping;

                db.Orders.AddRange(o1, o2, o3, o4, o5);
                await db.SaveChangesAsync();
            }
        }

        if (!await db.ChatMessages.AnyAsync())
        {
            var admin = await db.Users.FirstOrDefaultAsync(u => u.Email == "admin@alicraft.com");
            var demo  = await db.Users.FirstOrDefaultAsync(u => u.Email == "demo@alicraft.com");
            if (admin != null && demo != null)
            {
                db.ChatMessages.AddRange(
                    new ChatMessage { ThreadUserId = demo.Id, SenderId = demo.Id,  SenderRole = "User",
                        Body = "Hello! Can you customize a keychain with a photo of my cat?", CreatedAt = DateTime.UtcNow.AddDays(-2), IsRead = true },
                    new ChatMessage { ThreadUserId = demo.Id, SenderId = admin.Id, SenderRole = "Admin",
                        Body = "Of course! Please send us a clear well-lit photo and we'll prepare a preview.", CreatedAt = DateTime.UtcNow.AddDays(-2).AddMinutes(5), IsRead = true },
                    new ChatMessage { ThreadUserId = demo.Id, SenderId = demo.Id,  SenderRole = "User",
                        Body = "Thanks! When can I expect delivery?", CreatedAt = DateTime.UtcNow.AddHours(-1), IsRead = false }
                );
                await db.SaveChangesAsync();
            }
        }
    }

    /// <summary>
    /// Returns true only when the `Users` table exists (case-sensitive on Postgres).
    /// Used as a stricter alternative to <c>HasTablesAsync</c>, which returns true
    /// for any non-system table and is therefore fooled by Neon's preinstalled
    /// extension metadata into skipping our schema creation.
    /// </summary>
    private static async Task<bool> OurSchemaExistsAsync(AppDbContext db)
    {
        var conn = db.Database.GetDbConnection();
        var wasClosed = conn.State != System.Data.ConnectionState.Open;
        if (wasClosed) await conn.OpenAsync();
        try
        {
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = db.Database.IsSqlite()
                ? "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = 'Users'"
                : "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = 'public' AND table_name = 'Users'";
            var result = await cmd.ExecuteScalarAsync();
            return Convert.ToInt32(result) > 0;
        }
        finally
        {
            if (wasClosed) await conn.CloseAsync();
        }
    }

    /// <summary>
    /// Adds new columns to existing SQLite DBs that were created before email-verification fields were introduced.
    /// EnsureCreatedAsync only creates the schema once; it doesn't add columns to existing tables.
    /// Only applies to SQLite — Postgres starts fresh with the full schema so no legacy patching is needed.
    /// </summary>
    private static async Task EnsureSchemaUpgradesAsync(AppDbContext db)
    {
        // PRAGMA table_info is SQLite-specific. On Postgres this would error out
        // and isn't needed anyway since the schema is created correctly upfront.
        if (!db.Database.IsSqlite()) return;

        var conn = db.Database.GetDbConnection();
        if (conn.State != System.Data.ConnectionState.Open) await conn.OpenAsync();

        var existing = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        await using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "PRAGMA table_info('Users')";
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                existing.Add(reader.GetString(1));
            }
        }
        if (existing.Count == 0) return; // table doesn't exist yet, EnsureCreated will handle it

        async Task AddColumnIfMissing(string column, string type, string? defaultValue = null)
        {
            if (existing.Contains(column)) return;
            var def = defaultValue == null ? "" : $" DEFAULT {defaultValue}";
            await using var c = conn.CreateCommand();
            c.CommandText = $"ALTER TABLE Users ADD COLUMN \"{column}\" {type}{def}";
            await c.ExecuteNonQueryAsync();
        }

        await AddColumnIfMissing("IsEmailVerified", "INTEGER NOT NULL", "1"); // default true so existing users aren't locked out
        await AddColumnIfMissing("EmailVerificationToken", "TEXT NULL");
        await AddColumnIfMissing("EmailVerificationTokenExpiresAt", "TEXT NULL");
        await AddColumnIfMissing("EmailVerificationLastSentAt", "TEXT NULL");

        await AddColumnIfMissing("PasswordResetToken", "TEXT NULL");
        await AddColumnIfMissing("PasswordResetExpiresAt", "TEXT NULL");
        await AddColumnIfMissing("PasswordResetLastSentAt", "TEXT NULL");
    }
}
