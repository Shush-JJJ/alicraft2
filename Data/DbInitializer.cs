using Alicraft2.Models;
using Microsoft.EntityFrameworkCore;

namespace Alicraft2.Data;

public static class DbInitializer
{
    public static async Task SeedAsync(AppDbContext db)
    {
        await db.Database.EnsureCreatedAsync();
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

        if (!await db.Products.AnyAsync())
        {
            db.Products.AddRange(
                new Product
                {
                    Name = "Classic Wooden Lithophane Frame",
                    Description = "Handcrafted wooden frame with a custom 3D lithophane insert. LED backlight included. Ideal gift for anniversaries and birthdays.",
                    Category = "Frame",
                    Price = 799m,
                    Stock = 25,
                    ImagePath = "/images/placeholder-frame.svg"
                },
                new Product
                {
                    Name = "Heart-Shaped Lithophane Frame",
                    Description = "Romantic heart-shaped lithophane with USB-powered warm-white light. Send us the photo — we handle the rest.",
                    Category = "Frame",
                    Price = 699m,
                    Stock = 30,
                    ImagePath = "/images/placeholder-frame2.svg"
                },
                new Product
                {
                    Name = "Premium Acrylic Lithophane Frame",
                    Description = "Crystal-clear acrylic stand with crisp, high-detail 3D print. Modern minimalist look for offices and living rooms.",
                    Category = "Frame",
                    Price = 1_199m,
                    Stock = 15,
                    ImagePath = "/images/placeholder-frame3.svg"
                },
                new Product
                {
                    Name = "Personalized Lithophane Keychain",
                    Description = "Carry your favorite photo everywhere. Hold it to the light to reveal the 3D portrait. Comes with a sturdy metal ring.",
                    Category = "Keychain",
                    Price = 199m,
                    Stock = 100,
                    ImagePath = "/images/placeholder-keychain.svg"
                },
                new Product
                {
                    Name = "Couple Lithophane Keychain Set",
                    Description = "A matching pair for you and your special someone. Two keychains, one unforgettable story.",
                    Category = "Keychain",
                    Price = 349m,
                    Stock = 50,
                    ImagePath = "/images/placeholder-keychain2.svg"
                },
                new Product
                {
                    Name = "Pet Memorial Lithophane Keychain",
                    Description = "A thoughtful way to keep your furry friend close. Send us your pet's photo and we will craft it with love.",
                    Category = "Keychain",
                    Price = 249m,
                    Stock = 60,
                    ImagePath = "/images/placeholder-keychain3.svg"
                }
            );
            await db.SaveChangesAsync();
        }

        if (!await db.Orders.AnyAsync())
        {
            var demo = await db.Users.FirstOrDefaultAsync(u => u.Email == "demo@alicraft.com");
            var products = await db.Products.OrderBy(p => p.Id).ToListAsync();
            if (demo != null && products.Count >= 6)
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
                o1.Items.Add(new OrderItem { ProductId = products[0].Id, ProductName = products[0].Name, UnitPrice = products[0].Price, Quantity = 1 });
                o1.Items.Add(new OrderItem { ProductId = products[3].Id, ProductName = products[3].Name, UnitPrice = products[3].Price, Quantity = 2 });
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
                o2.Items.Add(new OrderItem { ProductId = products[1].Id, ProductName = products[1].Name, UnitPrice = products[1].Price, Quantity = 1 });
                o2.Items.Add(new OrderItem { ProductId = products[4].Id, ProductName = products[4].Name, UnitPrice = products[4].Price, Quantity = 1 });
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
                o3.Items.Add(new OrderItem { ProductId = products[2].Id, ProductName = products[2].Name, UnitPrice = products[2].Price, Quantity = 1 });
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
                o4.Items.Add(new OrderItem { ProductId = products[5].Id, ProductName = products[5].Name, UnitPrice = products[5].Price, Quantity = 2, CustomNote = "Please print with my dog Luna." });
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
                o5.Items.Add(new OrderItem { ProductId = products[3].Id, ProductName = products[3].Name, UnitPrice = products[3].Price, Quantity = 3 });
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
