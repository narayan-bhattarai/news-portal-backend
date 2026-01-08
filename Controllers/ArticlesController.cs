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
    public async Task<IEnumerable<Article>> Get([FromQuery] string? search, [FromQuery] string? category)
    {
        var query = _context.Articles.AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            search = search.ToLower();
            query = query.Where(a => a.Title.ToLower().Contains(search) || 
                                     a.Excerpt.ToLower().Contains(search) ||
                                     a.Category.ToLower().Contains(search));
        }

        if (!string.IsNullOrWhiteSpace(category))
        {
            // Case-insensitive exact match for category
            var lowerCategory = category.ToLower();
            query = query.Where(a => a.Category.ToLower() == lowerCategory);
        }

        return await query.OrderByDescending(a => a.Id).ToListAsync();
    }

    [HttpGet("trending")]
    public async Task<IEnumerable<Article>> GetTrending()
    {
        return await _context.Articles.Where(a => a.IsTrending).ToListAsync();
    }
    
    [HttpGet("editors-picks")]
    public async Task<IEnumerable<Article>> GetEditorsPicks()
    {
        return await _context.Articles.Where(a => a.IsTrending).Take(2).ToListAsync();
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetArticle(int id)
    {
        var article = await _context.Articles.FindAsync(id);

        if (article == null)
        {
            return NotFound();
        }

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
    public async Task<IActionResult> Update(int id, [FromBody] Article article)
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
    public async Task<IActionResult> Delete(int id)
    {
        var article = await _context.Articles.FindAsync(id);
        if (article == null) return NotFound();
        
        _context.Articles.Remove(article);
        await _context.SaveChangesAsync();
        
        return NoContent();
    }
}
