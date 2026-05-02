using Alicraft2.Models;
using Microsoft.EntityFrameworkCore;

namespace Alicraft2.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<CartItem> CartItems => Set<CartItem>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();
    public DbSet<ChatMessage> ChatMessages => Set<ChatMessage>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<User>().HasIndex(u => u.Email).IsUnique();
        b.Entity<Order>().HasIndex(o => o.OrderNumber).IsUnique();

        b.Entity<Product>().Property(p => p.Price).HasColumnType("decimal(18,2)");
        b.Entity<Order>().Property(p => p.Subtotal).HasColumnType("decimal(18,2)");
        b.Entity<Order>().Property(p => p.Shipping).HasColumnType("decimal(18,2)");
        b.Entity<Order>().Property(p => p.Total).HasColumnType("decimal(18,2)");
        b.Entity<OrderItem>().Property(p => p.UnitPrice).HasColumnType("decimal(18,2)");

        b.Entity<CartItem>()
            .HasOne(c => c.Product)
            .WithMany()
            .HasForeignKey(c => c.ProductId)
            .OnDelete(DeleteBehavior.Cascade);

        b.Entity<CartItem>()
            .HasOne(c => c.User)
            .WithMany(u => u.CartItems)
            .HasForeignKey(c => c.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        b.Entity<OrderItem>()
            .HasOne(i => i.Order)
            .WithMany(o => o.Items)
            .HasForeignKey(i => i.OrderId)
            .OnDelete(DeleteBehavior.Cascade);

        b.Entity<OrderItem>()
            .HasOne(i => i.Product)
            .WithMany()
            .HasForeignKey(i => i.ProductId)
            .OnDelete(DeleteBehavior.Restrict);

        b.Entity<ChatMessage>()
            .HasOne(m => m.ThreadUser)
            .WithMany()
            .HasForeignKey(m => m.ThreadUserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
