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

    [HttpGet]
    [Authorize]
    public async Task<IEnumerable<ChatMessage>> GetHistory()
    {
        var currentUser = User.Identity?.Name?.ToLower();
        if (string.IsNullOrEmpty(currentUser)) return new List<ChatMessage>();

        // We explicitly construct the comma-search pattern for safety
        var deleteToken = "," + currentUser + ",";

        // Return last 50 messages OR all unread messages for the user
        // We fetch a bit more to be safe, or split logic. 
        // Simple approach: Get unread first, then recent history, then combine.
        var unreadMessages = await _context.ChatMessages
            .Where(m => m.Receiver.ToLower() == currentUser && !m.IsRead && !m.DeletedFor.ToLower().Contains(deleteToken))
            .ToListAsync();

        var recentMessages = await _context.ChatMessages
            .Where(m => (m.Sender.ToLower() == currentUser || m.Receiver.ToLower() == currentUser) &&
                        !m.DeletedFor.ToLower().Contains(deleteToken))
            .OrderByDescending(m => m.Timestamp)
            .Take(50)
            .ToListAsync();
        
        return unreadMessages.Concat(recentMessages)
            .GroupBy(m => m.Id)
            .Select(g => g.First())
            .OrderBy(m => m.Timestamp);
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
        var currentUser = User.Identity?.Name?.ToLower();
        if (string.IsNullOrEmpty(currentUser)) return Unauthorized();
        var targetUser = username.ToLower();

        // 1. Find messages between these two users
        var messages = await _context.ChatMessages.Where(m => 
            (m.Sender.ToLower() == currentUser && m.Receiver.ToLower() == targetUser) || 
            (m.Sender.ToLower() == targetUser && m.Receiver.ToLower() == currentUser)
        ).ToListAsync();

        // 2. Soft Delete logic
        // 2. Soft Delete logic (Promote to Hard Delete if both sides deleted)
        var toDelete = new List<ChatMessage>();
        var toUpdate = new List<ChatMessage>();

        foreach (var msg in messages)
        {
            // Ensure DeletedFor is initialized
            if (msg.DeletedFor == null) msg.DeletedFor = "";

            var deleteToken = "," + currentUser + ",";

            // If not already marked as deleted for this user
            if (!msg.DeletedFor.ToLower().Contains(deleteToken))
            {
                // If it's empty, start with a comma
                if (string.IsNullOrEmpty(msg.DeletedFor))
                {
                    msg.DeletedFor = "," + currentUser + ",";
                }
                else
                {
                    // Append
                    msg.DeletedFor += currentUser + ",";
                }
            }

            // Check if BOTH Sender and Receiver have deleted it
            var senderToken = "," + msg.Sender.ToLower() + ",";
            var receiverToken = "," + msg.Receiver.ToLower() + ",";
            
            // Check using Case Insensitive string check
            var deletedForLower = msg.DeletedFor.ToLower();

            if (deletedForLower.Contains(senderToken) && deletedForLower.Contains(receiverToken))
            {
                toDelete.Add(msg);
            }
            else
            {
                toUpdate.Add(msg);
            }
        }

        // 3. Apply Changes
        if (toDelete.Any()) 
        {
            _context.ChatMessages.RemoveRange(toDelete);
        }
        if (toUpdate.Any())
        {
            _context.ChatMessages.UpdateRange(toUpdate);
        }
        
        await _context.SaveChangesAsync();
        return Ok();
    }
}
