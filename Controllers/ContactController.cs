using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NewsPortal.API.Data;
using NewsPortal.API.Models;

namespace NewsPortal.API.Controllers;

public class ContactMessage
{
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
}

[ApiController]
[Route("api/[controller]")]
public class ContactController : ControllerBase
{
    private readonly NewsContext _context;

    public ContactController(NewsContext context)
    {
        _context = context;
    }

    [HttpPost]
    public async Task<IActionResult> Post([FromBody] ContactMessage message)
    {
        var submission = new ContactSubmission
        {
            Name = message.Name,
            Email = message.Email,
            Phone = message.Phone,
            Message = message.Message,
            SubmittedAt = DateTime.UtcNow
        };

        _context.ContactSubmissions.Add(submission);
        await _context.SaveChangesAsync();

        return Ok(new { status = "success", message = "Message received successfully" });
    }

    [HttpGet]
    [Authorize]
    public async Task<ActionResult<IEnumerable<ContactSubmission>>> GetMessages()
    {
        return await _context.ContactSubmissions.OrderByDescending(m => m.SubmittedAt).ToListAsync();
    }

    [HttpDelete("{id}")]
    [Authorize]
    public async Task<IActionResult> DeleteMessage(Guid id)
    {
        var msg = await _context.ContactSubmissions.FindAsync(id);
        if (msg == null) return NotFound();

        _context.ContactSubmissions.Remove(msg);
        await _context.SaveChangesAsync();
        return NoContent();
    }
}
