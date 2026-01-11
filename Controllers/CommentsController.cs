using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NewsPortal.API.Data;
using NewsPortal.API.Models;

namespace NewsPortal.API.Controllers;

[Route("api/[controller]")]
[ApiController]
public class CommentsController : ControllerBase
{
    private readonly NewsContext _context;

    public CommentsController(NewsContext context)
    {
        _context = context;
    }

    // DTOs
    public class CommentDto
    {
        public Guid Id { get; set; }
        public Guid ArticleId { get; set; }
        public string Username { get; set; } = string.Empty;
        public string? UserProfileImage { get; set; }
        public string Content { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public bool IsOwner { get; set; } // If current user is owner (Helper for frontend)
    }

    public class CreateCommentDto
    {
        public Guid ArticleId { get; set; }
        public string Content { get; set; } = string.Empty;
    }

    [HttpGet("article/{articleId}")]
    public async Task<ActionResult<IEnumerable<CommentDto>>> GetComments(Guid articleId)
    {
        var currentUsername = User.Identity?.Name;
        
        // Fetch raw data first to avoid EF translation issues with string complex logic (like Split)
        var rawComments = await _context.Comments.AsNoTracking()
            .Where(c => c.ArticleId == articleId)
            .Include(c => c.User)
            .OrderByDescending(c => c.CreatedAt)
            .ToListAsync();

        var comments = rawComments.Select(c => {
            string displayName = "Unknown User";
            if (c.User != null)
            {
                // FORCE: Check Full Name first
                if (!string.IsNullOrWhiteSpace(c.User.FullName))
                {
                    displayName = c.User.FullName;
                    Console.WriteLine($"[DEBUG] Using FullName for comment {c.Id}: {displayName}");
                }
                else
                {
                    // Fallback: Strip email from Username
                    displayName = c.User.UserName ?? "Unknown";
                    if (displayName.Contains("@")) 
                    {
                        displayName = displayName.Split('@')[0];
                    }
                    Console.WriteLine($"[DEBUG] Falling back to stripped email for comment {c.Id}: {displayName}");
                }
            }

            return new CommentDto
            {
                Id = c.Id,
                ArticleId = c.ArticleId,
                Username = displayName,
                UserProfileImage = c.User?.ProfileImage,
                Content = c.Content,
                CreatedAt = c.CreatedAt,
                IsOwner = currentUsername != null && c.User != null && c.User.UserName == currentUsername
            };
        }).ToList();

        return Ok(comments);
    }

    [HttpPost]
    [Authorize]
    public async Task<ActionResult<CommentDto>> PostComment([FromBody] CreateCommentDto request)
    {
        if (string.IsNullOrWhiteSpace(request.Content))
            return BadRequest("Content is required.");

        var username = User.Identity?.Name;
        if (string.IsNullOrEmpty(username)) return Unauthorized();

        var user = await _context.Users.FirstOrDefaultAsync(u => u.UserName == username);
        if (user == null) return Unauthorized();

        // Security check: Only 'Viewer' role can post comments. Staff (Admin/Editor) cannot.
        if (!User.IsInRole("Viewer"))
        {
            return Forbid("Only readers with the Viewer role can post comments. Staff accounts are restricted.");
        }

        // Verify Article Exists
        var articleExists = await _context.Articles.AnyAsync(a => a.Id == request.ArticleId);
        if (!articleExists) return NotFound("Article not found.");

        var comment = new Comment
        {
            Id = Guid.NewGuid(),
            ArticleId = request.ArticleId,
            UserId = user.Id,
            Content = request.Content,
            CreatedAt = DateTime.UtcNow
        };

        _context.Comments.Add(comment);
        await _context.SaveChangesAsync();

        // Determine display name
        string postedBy = !string.IsNullOrWhiteSpace(user.FullName) ? user.FullName : (user.UserName ?? "Unknown");
        if (postedBy.Contains("@")) postedBy = postedBy.Split('@')[0];

        var dto = new CommentDto
        {
            Id = comment.Id,
            ArticleId = comment.ArticleId,
            Username = postedBy,
            UserProfileImage = user.ProfileImage,
            Content = comment.Content,
            CreatedAt = comment.CreatedAt,
            IsOwner = true
        };

        return CreatedAtAction(nameof(GetComments), new { articleId = request.ArticleId }, dto);
    }

    [HttpDelete("{id}")]
    [Authorize]
    public async Task<IActionResult> DeleteComment(Guid id)
    {
        var username = User.Identity?.Name;
        if (string.IsNullOrEmpty(username)) return Unauthorized();

        var comment = await _context.Comments.Include(c => c.User).FirstOrDefaultAsync(c => c.Id == id);
        if (comment == null) return NotFound();

        // Check Authorization: Admin OR Owner
        var isOwner = comment.User != null && comment.User.UserName == username;
        var isAdmin = User.IsInRole("Admin");

        if (!isOwner && !isAdmin)
        {
            return Forbid();
        }

        _context.Comments.Remove(comment);
        await _context.SaveChangesAsync();

        return NoContent();
    }
}
