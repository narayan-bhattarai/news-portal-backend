
namespace NewsPortal.API.Models;

public class ChatMessage
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SenderUserId { get; set; }
    public Guid ReceiverUserId { get; set; } // Added ReceiverUserId
    public string Content { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public bool IsRead { get; set; } = false;
    public string DeletedFor { get; set; } = string.Empty; // Comma-separated UserIds
}
