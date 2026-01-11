using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
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
            var query = _context.Articles.AsNoTracking().Include(a => a.Category).AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                query = query.Where(a => EF.Functions.ILike(a.Title, $"%{search}%") || 
                                         EF.Functions.ILike(a.Excerpt, $"%{search}%") ||
                                         (a.Category != null && EF.Functions.ILike(a.Category.Name, $"%{search}%")));
            }

            if (!string.IsNullOrWhiteSpace(category))
            {
                query = query.Where(a => a.Category != null && EF.Functions.ILike(a.Category.Name, category));
            }

            var totalCount = await query.CountAsync();
            var results = await query.OrderByDescending(a => a.PublishedAt)
                                     .Skip((page - 1) * pageSize)
                                     .Take(pageSize)
                                     .Select(a => new {
                                         a.Id,
                                         Category = a.Category != null ? a.Category.Name : null,
                                         a.Title,
                                         a.Excerpt,
                                         a.Author,
                                         a.ImageUrl,
                                         a.Content,
                                         a.IsTrending,
                                         a.IsEditorsPick,
                                         a.IsFeatured,
                                         a.ViewCount,
                                         CommentCount = a.Comments.Count(),
                                         a.PublishedAt
                                     })
                                     .ToListAsync();

            return Ok(new { Items = results, TotalCount = totalCount, Page = page, PageSize = pageSize });
        }
        catch (Exception ex)
        {
            Serilog.Log.Error(ex, "Database error fetching articles.");
            return StatusCode(500, new { Message = "Database connection error.", Detail = ex.Message });
        }
    }

    [HttpGet("trending")]
    public async Task<IEnumerable<object>> GetTrending()
    {
        return await _context.Articles.AsNoTracking()
            .Where(a => a.IsTrending)
            .Include(a => a.Category)
            .Select(a => new {
                 a.Id,
                 Category = a.Category != null ? a.Category.Name : null,
                 a.Title,
                 a.Excerpt,
                 a.Author,
                 a.ImageUrl,
                 a.Content,
                 a.IsTrending,
                 a.IsEditorsPick,
                 a.IsFeatured,
                 a.ViewCount,
                 CommentCount = a.Comments.Count(),
                 a.PublishedAt
             })
            .ToListAsync();
    }
    
    [HttpGet("editors-picks")]
    public async Task<IEnumerable<object>> GetEditorsPicks()
    {
        return await _context.Articles.AsNoTracking()
            .Where(a => a.IsEditorsPick)
            .Include(a => a.Category)
            .Take(4)
            .Select(a => new {
                 a.Id,
                 Category = a.Category != null ? a.Category.Name : null,
                 a.Title,
                 a.Excerpt,
                 a.Author,
                 a.ImageUrl,
                 a.Content,
                 a.IsTrending,
                 a.IsEditorsPick,
                 a.IsFeatured,
                 a.ViewCount,
                 CommentCount = a.Comments.Count(),
                 a.PublishedAt
             })
            .ToListAsync();
    }

    [HttpGet("featured")]
    public async Task<object?> GetFeatured()
    {
        var article = await _context.Articles.AsNoTracking()
            .Include(a => a.Category)
            .Include(a => a.Comments)
            .FirstOrDefaultAsync(a => a.IsFeatured);
            
        if (article == null) return null;
        
        return new {
             article.Id,
             Category = article.Category != null ? article.Category.Name : null,
             article.Title,
             article.Excerpt,
             article.Author,
             article.ImageUrl,
             article.Content,
             article.IsTrending,
             article.IsEditorsPick,
             article.IsFeatured,
             article.ViewCount,
             CommentCount = article.Comments.Count(),
             article.PublishedAt
        };
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetArticle(Guid id)
    {
        var article = await _context.Articles
            .Include(a => a.Category)
            .Include(a => a.Comments)
            .FirstOrDefaultAsync(a => a.Id == id);

        if (article == null)
        {
            return NotFound();
        }


        article.ViewCount++;
        await _context.SaveChangesAsync();
        
        return Ok(new {
             article.Id,
             Category = article.Category != null ? article.Category.Name : null,
             article.Title,
             article.Excerpt,
             article.Author,
             article.ImageUrl,
             article.Content,
             article.IsTrending,
             article.IsEditorsPick,
             article.IsFeatured,
             article.ViewCount,
             CommentCount = article.Comments.Count(),
             article.PublishedAt
        });
    }

    [HttpPost]
    [Authorize(Roles = "Admin,Editor")]
    public async Task<IActionResult> Create([FromBody] ArticleDto articleDto)
    {

        var category = await _context.Categories.FirstOrDefaultAsync(c => c.Name == articleDto.Category);
        if (category == null) 
        {

             return BadRequest($"Category '{articleDto.Category}' not found.");
        }

        var article = new Article
        {
            Title = articleDto.Title,
            CategoryId = category.Id,
            Excerpt = articleDto.Excerpt,
            Author = articleDto.Author,
            ImageUrl = articleDto.ImageUrl,
            Content = articleDto.Content,
            IsTrending = articleDto.IsTrending,
            IsEditorsPick = articleDto.IsEditorsPick,
            IsFeatured = articleDto.IsFeatured,
            PublishedAt = DateTime.UtcNow
        };
        
        _context.Articles.Add(article);
        await _context.SaveChangesAsync();
        
        return Ok(article);
    }

    [HttpPut("{id}")]
    [Authorize(Roles = "Admin,Editor")]
    public async Task<IActionResult> Update(Guid id, [FromBody] ArticleDto articleDto)
    {


        var existingArticle = await _context.Articles.FindAsync(id);
        if (existingArticle == null) return NotFound();


        var category = await _context.Categories.FirstOrDefaultAsync(c => c.Name == articleDto.Category);
        if (category == null) return BadRequest("Invalid Category");

        existingArticle.Title = articleDto.Title;
        existingArticle.CategoryId = category.Id; 
        existingArticle.Author = articleDto.Author;
        existingArticle.ImageUrl = articleDto.ImageUrl;
        existingArticle.Excerpt = articleDto.Excerpt;
        existingArticle.Content = articleDto.Content; 
        existingArticle.IsTrending = articleDto.IsTrending;
        existingArticle.IsEditorsPick = articleDto.IsEditorsPick;
        existingArticle.IsFeatured = articleDto.IsFeatured;
        
        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            if (!_context.Articles.Any(e => e.Id == id)) return NotFound();
            else throw;
        }

        return NoContent();
    }

    public class ArticleDto
    {
        public Guid? Id { get; set; }
        public string Title { get; set; } = "";
        public string Category { get; set; } = "";
        public string Excerpt { get; set; } = "";
        public string Author { get; set; } = "";
        public string ImageUrl { get; set; } = "";
        public string Content { get; set; } = "";
        public bool IsTrending { get; set; }
        public bool IsEditorsPick { get; set; }
        public bool IsFeatured { get; set; }
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = "Admin,Editor")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var article = await _context.Articles.FindAsync(id);
        if (article == null) return NotFound();
        
        _context.Articles.Remove(article);
        await _context.SaveChangesAsync();
        
        return NoContent();
    }
}
