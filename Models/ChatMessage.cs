namespace Alicraft2.Models;

public class ChatMessage
{
    public int Id { get; set; }

    // Thread owner = the customer's UserId.
    public int ThreadUserId { get; set; }
    public User? ThreadUser { get; set; }

    public int SenderId { get; set; }
    public string SenderRole { get; set; } = "User"; // User | Admin

    public string Body { get; set; } = string.Empty;
    public string? ImagePath { get; set; }

    public bool IsRead { get; set; } = false;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
