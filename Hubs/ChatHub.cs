using Microsoft.AspNetCore.SignalR;
using NewsPortal.API.Models;
using NewsPortal.API.Data;

namespace NewsPortal.API.Hubs;

public class ChatHub : Hub
{
    private readonly NewsContext _context;

    public ChatHub(NewsContext context)
    {
        _context = context;
    }

    public async Task SendMessage(string sender, string receiver, string message)
    {
        var chatMsg = new ChatMessage
        {
            Sender = sender,
            Receiver = receiver,
            Content = message,
            Timestamp = DateTime.UtcNow
        };

        _context.ChatMessages.Add(chatMsg);
        await _context.SaveChangesAsync();

        // Broadcast to everyone for simplicity in this MVP, 
        // purely so "filtering" on frontend works without complex connection mapping.
        // In prod, use Clients.User(receiverId).
        await Clients.All.SendAsync("ReceiveMessage", sender, receiver, message, chatMsg.Timestamp);
    }

    public async Task MarkAsRead(string reader, string senderOfMessages)
    {
        var unread = _context.ChatMessages
            .Where(m => m.Receiver.ToLower() == reader.ToLower() && m.Sender.ToLower() == senderOfMessages.ToLower() && !m.IsRead)
            .ToList();

        if (unread.Any())
        {
            foreach (var msg in unread)
            {
                msg.IsRead = true;
            }
            await _context.SaveChangesAsync();
            // Notify reader so they can update UI (optional but good)
            await Clients.All.SendAsync("MessagesRead", reader, senderOfMessages); 
        }
    }
}
