using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NewsPortal.API.Data;
using NewsPortal.API.Models;
using Microsoft.AspNetCore.Authorization;

namespace NewsPortal.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PagesController : ControllerBase
{
    private readonly NewsContext _context;

    public PagesController(NewsContext context)
    {
        _context = context;
    }

    [HttpGet("{slug}")]
    public async Task<IActionResult> Get(string slug)
    {
        var page = await _context.Pages.FindAsync(slug);
        if (page == null) return NotFound();
        return Ok(page);
    }

    [HttpPut("{slug}")]
    [Authorize]
    public async Task<IActionResult> Update(string slug, [FromBody] PageContent content)
    {
        if (slug != content.Slug) return BadRequest();

        var existing = await _context.Pages.FindAsync(slug);
        if (existing == null)
        {
            // Allow creating if doesn't exist? Or restrictive? Let's allow create/update (upsert) behavior or just create if missing.
             content.LastUpdated = DateTime.UtcNow;
            _context.Pages.Add(content);
        }
        else
        {
            existing.Title = content.Title;
            existing.Body = content.Body;
            existing.LastUpdated = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync();
        return Ok(content);
    }
}
