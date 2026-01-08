using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
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

    public async Task SendMessage(string receiverUsername, string message)
    {
        var senderUsername = Context.User?.Identity?.Name;
        if (string.IsNullOrEmpty(senderUsername)) return;

        var sender = await _context.Users.FirstOrDefaultAsync(u => u.Username == senderUsername);
        var receiver = await _context.Users.FirstOrDefaultAsync(u => u.Username == receiverUsername);

        if (sender == null || receiver == null) return;

        var chatMsg = new ChatMessage
        {
            SenderUserId = sender.Id,
            ReceiverUserId = receiver.Id,
            Content = message,
            Timestamp = DateTime.UtcNow,
            DeletedFor = ""
        };

        _context.ChatMessages.Add(chatMsg);
        await _context.SaveChangesAsync();

        // Broadcast with usernames for UI display, while using IDs for DB
        await Clients.All.SendAsync("ReceiveMessage", senderUsername, receiverUsername, message, chatMsg.Timestamp);
    }

    public async Task MarkAsRead(string senderOfMessagesUsername)
    {
        var readerUsername = Context.User?.Identity?.Name;
        if (string.IsNullOrEmpty(readerUsername)) return;

        var reader = await _context.Users.FirstOrDefaultAsync(u => u.Username == readerUsername);
        var sender = await _context.Users.FirstOrDefaultAsync(u => u.Username == senderOfMessagesUsername);

        if (reader == null || sender == null) return;

        var unread = await _context.ChatMessages
            .Where(m => m.ReceiverUserId == reader.Id && m.SenderUserId == sender.Id && !m.IsRead)
            .ToListAsync();

        if (unread.Any())
        {
            foreach (var msg in unread)
            {
                msg.IsRead = true;
            }
            await _context.SaveChangesAsync();
            await Clients.All.SendAsync("MessagesRead", readerUsername, senderOfMessagesUsername); 
        }
    }
}
