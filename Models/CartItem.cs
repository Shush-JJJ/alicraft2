namespace Alicraft2.Models;

public class CartItem
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public User? User { get; set; }

    public int ProductId { get; set; }
    public Product? Product { get; set; }

    public int Quantity { get; set; } = 1;

    public string? CustomNote { get; set; }
    public string? CustomImagePath { get; set; }

    public DateTime AddedAt { get; set; } = DateTime.UtcNow;
}
