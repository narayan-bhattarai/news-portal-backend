using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NewsPortal.API.Data;
using NewsPortal.API.Models;

namespace NewsPortal.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ArticlesController : ControllerBase
{
    private readonly NewsContext _context;

    public ArticlesController(NewsContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] string? search, [FromQuery] string? category, [FromQuery] int page = 1, [FromQuery] int pageSize = 10)
    {
        try
        {
            var query = _context.Articles.AsNoTracking().AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                query = query.Where(a => EF.Functions.ILike(a.Title, $"%{search}%") || 
                                         EF.Functions.ILike(a.Excerpt, $"%{search}%") ||
                                         EF.Functions.ILike(a.Category, $"%{search}%"));
            }

            if (!string.IsNullOrWhiteSpace(category))
            {
                query = query.Where(a => EF.Functions.ILike(a.Category, category));
            }

            var totalCount = await query.CountAsync();
            var results = await query.OrderByDescending(a => a.PublishedAt)
                                     .Skip((page - 1) * pageSize)
                                     .Take(pageSize)
                                     .ToListAsync();

            return Ok(new { Items = results, TotalCount = totalCount, Page = page, PageSize = pageSize });
        }
        catch (Exception ex)
        {
            Serilog.Log.Error(ex, "Database error fetching articles. This is often a timeout on Render's free tier.");
            return StatusCode(500, new { Message = "Database connection error (Possible timeout). Please wait 30 seconds and refresh.", Detail = ex.Message });
        }
    }

    [HttpGet("trending")]
    public async Task<IEnumerable<Article>> GetTrending()
    {
        return await _context.Articles.AsNoTracking().Where(a => a.IsTrending).ToListAsync();
    }
    
    [HttpGet("editors-picks")]
    public async Task<IEnumerable<Article>> GetEditorsPicks()
    {
        // Now using the dedicated flag
        return await _context.Articles.AsNoTracking().Where(a => a.IsEditorsPick).Take(4).ToListAsync();
    }

    [HttpGet("featured")]
    public async Task<Article?> GetFeatured()
    {
        return await _context.Articles.AsNoTracking().FirstOrDefaultAsync(a => a.IsFeatured);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetArticle(Guid id)
    {
        var article = await _context.Articles.FindAsync(id);

        if (article == null)
        {
            return NotFound();
        }

        // Increment View Count
        article.ViewCount++;
        await _context.SaveChangesAsync();
        
        return Ok(article);
    }

    [HttpPost]
    [Microsoft.AspNetCore.Authorization.Authorize]
    public async Task<IActionResult> Create([FromBody] Article article)
    {
        article.PublishedAt = DateTime.UtcNow; 
        
        _context.Articles.Add(article);
        await _context.SaveChangesAsync();
        
        return Ok(article);
    }

    [HttpPut("{id}")]
    [Microsoft.AspNetCore.Authorization.Authorize]
    public async Task<IActionResult> Update(Guid id, [FromBody] Article article)
    {
        if (id != article.Id)
        {
            return BadRequest();
        }

        var existingArticle = await _context.Articles.FindAsync(id);
        
        if (existingArticle == null)
        {
            return NotFound();
        }

        existingArticle.Title = article.Title;
        existingArticle.Category = article.Category;
        existingArticle.Author = article.Author;
        existingArticle.ImageUrl = article.ImageUrl;
        existingArticle.Excerpt = article.Excerpt;
        existingArticle.Content = article.Content; // Update content
        existingArticle.IsTrending = article.IsTrending;
        existingArticle.IsEditorsPick = article.IsEditorsPick;
        existingArticle.IsFeatured = article.IsFeatured;
        
        // Don't update Id

        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            if (!_context.Articles.Any(e => e.Id == id))
            {
                return NotFound();
            }
            else
            {
                throw;
            }
        }

        return NoContent();
    }

    [HttpDelete("{id}")]
    [Microsoft.AspNetCore.Authorization.Authorize]
    public async Task<IActionResult> Delete(Guid id)
    {
        var article = await _context.Articles.FindAsync(id);
        if (article == null) return NotFound();
        
        _context.Articles.Remove(article);
        await _context.SaveChangesAsync();
        
        return NoContent();
    }
}
