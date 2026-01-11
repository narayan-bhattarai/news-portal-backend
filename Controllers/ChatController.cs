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
        public Guid Id { get; set; }
        public string Sender { get; set; } = string.Empty;
        public string Receiver { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
        public bool IsRead { get; set; }
    }

    [HttpGet]
    [Authorize(Roles = "Admin,Editor")]
    public async Task<IActionResult> GetHistory()
    {
        try
        {
            var username = User.Identity?.Name;
            if (string.IsNullOrEmpty(username)) return Ok(new List<MessageDto>());

            var user = await _context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.UserName == username);
            if (user == null) return Ok(new List<MessageDto>());

            var currentUserId = user.Id;
            var deleteToken = "," + currentUserId.ToString() + ",";


            var allMessages = await _context.ChatMessages.AsNoTracking()
                .Where(m => (m.SenderUserId == currentUserId || m.ReceiverUserId == currentUserId))
                .OrderByDescending(m => m.Timestamp)
                .Take(100)
                .ToListAsync();


            var filteredMessages = allMessages
                .Where(m => string.IsNullOrEmpty(m.DeletedFor) || !m.DeletedFor.Contains(deleteToken))
                .ToList();

            var userIds = filteredMessages.SelectMany(m => new[] { m.SenderUserId, m.ReceiverUserId }).Distinct().ToList();
            var userMap = await _context.Users.AsNoTracking()
                .Where(u => userIds.Contains(u.Id))
                .ToDictionaryAsync(u => u.Id, u => u.UserName);

            var result = filteredMessages.Select(m => new MessageDto
            {
                Id = m.Id,
                Sender = userMap.GetValueOrDefault(m.SenderUserId) ?? "Unknown",
                Receiver = userMap.GetValueOrDefault(m.ReceiverUserId) ?? "Unknown",
                Content = m.Content,
                Timestamp = m.Timestamp,
                IsRead = m.IsRead
            }).OrderBy(m => m.Timestamp).ToList();

            return Ok(result);
        }
        catch (Exception ex)
        {
            Serilog.Log.Error(ex, "Error fetching chat history.");
            return StatusCode(500, new { Message = "Failed to load chat history. Check if ChatMessages table exists and has ReceiverUserId column.", Detail = ex.Message });
        }
    }

    [HttpDelete]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> ClearAllHistory()
    {

        _context.ChatMessages.RemoveRange(_context.ChatMessages);
        await _context.SaveChangesAsync();
        return Ok();
    }

    [HttpDelete("{username}")]
    [Authorize(Roles = "Admin,Editor")]
    public async Task<IActionResult> DeleteConversation(string username)
    {
        var currentUsername = User.Identity?.Name;
        if (string.IsNullOrEmpty(currentUsername)) return Unauthorized();

        var currentUser = await _context.Users.FirstOrDefaultAsync(u => u.UserName == currentUsername);
        var targetUser = await _context.Users.FirstOrDefaultAsync(u => u.UserName == username);

        if (currentUser == null || targetUser == null) return NotFound("User not found");

        var currentId = currentUser.Id;
        var targetId = targetUser.Id;


        var messages = await _context.ChatMessages.Where(m => 
            (m.SenderUserId == currentId && m.ReceiverUserId == targetId) || 
            (m.SenderUserId == targetId && m.ReceiverUserId == currentId)
        ).ToListAsync();


        var toDelete = new List<ChatMessage>();
        var toUpdate = new List<ChatMessage>();

        foreach (var msg in messages)
        {
            if (msg.DeletedFor == null) msg.DeletedFor = "";

            var deleteToken = "," + currentId.ToString() + ",";

            if (!msg.DeletedFor.Contains(deleteToken))
            {
                if (string.IsNullOrEmpty(msg.DeletedFor))
                {
                    msg.DeletedFor = "," + currentId.ToString() + ",";
                }
                else
                {
                    msg.DeletedFor += currentId.ToString() + ",";
                }
            }


            var senderToken = "," + msg.SenderUserId.ToString() + ",";
            var receiverToken = "," + msg.ReceiverUserId.ToString() + ",";
            
            if (msg.DeletedFor.Contains(senderToken) && msg.DeletedFor.Contains(receiverToken))
            {
                toDelete.Add(msg);
            }
            else
            {
                toUpdate.Add(msg);
            }
        }


        if (toDelete.Any()) _context.ChatMessages.RemoveRange(toDelete);
        if (toUpdate.Any()) _context.ChatMessages.UpdateRange(toUpdate);
        
        await _context.SaveChangesAsync();
        return Ok();
    }
}
