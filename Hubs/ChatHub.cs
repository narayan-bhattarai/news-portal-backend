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

    public override async Task OnConnectedAsync()
    {
        var username = Context.User?.Identity?.Name;
        if (!string.IsNullOrEmpty(username))
        {
            // Add user to their own private group (lowercased for consistency)
            string groupName = username.Trim().ToLower();
            await Groups.AddToGroupAsync(Context.ConnectionId, groupName);
            Console.WriteLine($"[ChatHub] User '{username}' joined group '{groupName}'");
        }
        else 
        {
             Console.WriteLine("[ChatHub] Warning: Connection established without identity.");
        }
        await base.OnConnectedAsync();
    }

    public async Task SendMessage(string receiverUsername, string message)
    {
        var senderUsername = Context.User?.Identity?.Name;
        if (string.IsNullOrEmpty(senderUsername))
        {
            Console.WriteLine("[ChatHub] SendMessage failed: Anonymous connection.");
            return;
        }

        // Case-insensitive lookups for security and reliability
        var sender = await _context.Users.FirstOrDefaultAsync(u => u.UserName.ToLower() == senderUsername.ToLower());
        var receiver = await _context.Users.FirstOrDefaultAsync(u => u.UserName.ToLower() == receiverUsername.ToLower());

        if (sender == null || receiver == null)
        {
            Console.WriteLine($"[ChatHub] SendMessage ERROR: User lookup failed. Sender: '{senderUsername}' (found: {sender!=null}), Receiver: '{receiverUsername}' (found: {receiver!=null})");
            return;
        }

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

        // Send to participants using lowercased group names
        string senderGroup = senderUsername.Trim().ToLower();
        string receiverGroup = receiverUsername.Trim().ToLower();

        await Clients.Group(receiverGroup).SendAsync("ReceiveMessage", senderUsername, receiverUsername, message, chatMsg.Timestamp);
        await Clients.Group(senderGroup).SendAsync("ReceiveMessage", senderUsername, receiverUsername, message, chatMsg.Timestamp);
        
        Console.WriteLine($"[ChatHub] Message delivered: {senderUsername} -> {receiverUsername}");
    }

    public async Task MarkAsRead(string senderOfMessagesUsername)
    {
        var readerUsername = Context.User?.Identity?.Name;
        if (string.IsNullOrEmpty(readerUsername)) return;

        var reader = await _context.Users.FirstOrDefaultAsync(u => u.UserName == readerUsername);
        var sender = await _context.Users.FirstOrDefaultAsync(u => u.UserName == senderOfMessagesUsername);

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
            
            // Notify participants
            string readerGroup = readerUsername.Trim().ToLower();
            string senderGroup = senderOfMessagesUsername.Trim().ToLower();

            await Clients.Group(readerGroup).SendAsync("MessagesRead", readerUsername, senderOfMessagesUsername);
            await Clients.Group(senderGroup).SendAsync("MessagesRead", readerUsername, senderOfMessagesUsername);
        }
    }
}
