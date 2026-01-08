
namespace NewsPortal.API.Models;

public class ChatMessage
{
    public int Id { get; set; }
    public int SenderUserId { get; set; }
    public int ReceiverUserId { get; set; } // Added ReceiverUserId
    public string Content { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public bool IsRead { get; set; } = false;
    public string DeletedFor { get; set; } = string.Empty; // Comma-separated UserIds
}
