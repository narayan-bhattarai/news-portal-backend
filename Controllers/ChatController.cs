using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NewsPortal.API.Data;
using NewsPortal.API.Models;
using Microsoft.AspNetCore.Authorization;

namespace NewsPortal.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ChatController : ControllerBase
{
    private readonly NewsContext _context;

    public ChatController(NewsContext context)
    {
        _context = context;
    }

    public class MessageDto
    {
        public int Id { get; set; }
        public string Sender { get; set; } = string.Empty;
        public string Receiver { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
        public bool IsRead { get; set; }
    }

    [HttpGet]
    [Authorize]
    public async Task<IEnumerable<MessageDto>> GetHistory()
    {
        var username = User.Identity?.Name;
        if (string.IsNullOrEmpty(username)) return new List<MessageDto>();

        var user = await _context.Users.FirstOrDefaultAsync(u => u.Username == username);
        if (user == null) return new List<MessageDto>();

        var currentUserId = user.Id;
        var deleteToken = "," + currentUserId + ",";

        var allMessages = await _context.ChatMessages
            .Where(m => (m.SenderUserId == currentUserId || m.ReceiverUserId == currentUserId) &&
                        !m.DeletedFor.Contains(deleteToken))
            .OrderByDescending(m => m.Timestamp)
            .Take(100)
            .ToListAsync();

        var userIds = allMessages.SelectMany(m => new[] { m.SenderUserId, m.ReceiverUserId }).Distinct().ToList();
        var userMap = await _context.Users
            .Where(u => userIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.Username);

        return allMessages.Select(m => new MessageDto
        {
            Id = m.Id,
            Sender = userMap.GetValueOrDefault(m.SenderUserId) ?? "Unknown",
            Receiver = userMap.GetValueOrDefault(m.ReceiverUserId) ?? "Unknown",
            Content = m.Content,
            Timestamp = m.Timestamp,
            IsRead = m.IsRead
        }).OrderBy(m => m.Timestamp);
    }

    [HttpDelete]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> ClearAllHistory()
    {
        // Admin Force Clear - actually remove data? 
        // Or should this also be soft delete?
        // "ClearAllHistory" usually implies a hard reset for admin/testing. 
        // Keep as hard delete for now to maintain existing behavior for "Clear All", or implement logic later.
        // Assuming strict "Delete Conversation" request, we won't touch this endpoint deeply unless asked.
        _context.ChatMessages.RemoveRange(_context.ChatMessages);
        await _context.SaveChangesAsync();
        return Ok();
    }

    [HttpDelete("{username}")]
    [Authorize]
    public async Task<IActionResult> DeleteConversation(string username)
    {
        var currentUsername = User.Identity?.Name;
        if (string.IsNullOrEmpty(currentUsername)) return Unauthorized();

        var currentUser = await _context.Users.FirstOrDefaultAsync(u => u.Username == currentUsername);
        var targetUser = await _context.Users.FirstOrDefaultAsync(u => u.Username == username);

        if (currentUser == null || targetUser == null) return NotFound("User not found");

        var currentId = currentUser.Id;
        var targetId = targetUser.Id;

        // 1. Find messages between these two users
        var messages = await _context.ChatMessages.Where(m => 
            (m.SenderUserId == currentId && m.ReceiverUserId == targetId) || 
            (m.SenderUserId == targetId && m.ReceiverUserId == currentId)
        ).ToListAsync();

        // 2. Soft Delete logic
        var toDelete = new List<ChatMessage>();
        var toUpdate = new List<ChatMessage>();

        foreach (var msg in messages)
        {
            if (msg.DeletedFor == null) msg.DeletedFor = "";

            var deleteToken = "," + currentId + ",";

            if (!msg.DeletedFor.Contains(deleteToken))
            {
                if (string.IsNullOrEmpty(msg.DeletedFor))
                {
                    msg.DeletedFor = "," + currentId + ",";
                }
                else
                {
                    msg.DeletedFor += currentId + ",";
                }
            }

            // Check if BOTH Sender and Receiver have deleted it
            var senderToken = "," + msg.SenderUserId + ",";
            var receiverToken = "," + msg.ReceiverUserId + ",";
            
            if (msg.DeletedFor.Contains(senderToken) && msg.DeletedFor.Contains(receiverToken))
            {
                toDelete.Add(msg);
            }
            else
            {
                toUpdate.Add(msg);
            }
        }

        // 3. Apply Changes
        if (toDelete.Any()) _context.ChatMessages.RemoveRange(toDelete);
        if (toUpdate.Any()) _context.ChatMessages.UpdateRange(toUpdate);
        
        await _context.SaveChangesAsync();
        return Ok();
    }
}
