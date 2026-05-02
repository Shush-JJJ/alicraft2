using System.ComponentModel.DataAnnotations;

namespace Alicraft2.Models;

public class Product
{
    public int Id { get; set; }

    [Required, StringLength(120)]
    public string Name { get; set; } = string.Empty;

    [Required]
    public string Description { get; set; } = string.Empty;

    [Required]
    public string Category { get; set; } = "Frame"; // Frame | Keychain

    [Range(0, 1_000_000)]
    public decimal Price { get; set; }

    [Range(0, 100_000)]
    public int Stock { get; set; }

    public string? ImagePath { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
