using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NewsPortal.API.Data;
using NewsPortal.API.Models;
using Microsoft.AspNetCore.Authorization;

namespace NewsPortal.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CategoriesController : ControllerBase
{
    private readonly NewsContext _context;

    public CategoriesController(NewsContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> Get()
    {
        try
        {
            var categories = await _context.Categories.AsNoTracking().ToListAsync();
            
            // Get article counts for each category
            var categoryCounts = await _context.Articles
                .GroupBy(a => a.Category)
                .Select(g => new { Category = g.Key, Count = g.Count() })
                .ToListAsync();

            var result = categories.Select(c => new {
                c.Id,
                c.Name,
                ArticleCount = categoryCounts.FirstOrDefault(cc => cc.Category == c.Name)?.Count ?? 0
            }).OrderByDescending(c => c.ArticleCount).ToList();

            return Ok(result);
        }
        catch (Exception ex)
        {
            Serilog.Log.Error(ex, "Error fetching categories");
            return StatusCode(500, new { Message = "Failed to load categories. Possible database timeout. Please refresh.", Detail = ex.Message });
        }
    }

    [HttpPost]
    [Authorize]
    public async Task<IActionResult> Create([FromBody] Category category)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(category.Name))
            {
                return BadRequest(new { Message = "Category name is required." });
            }

            // Case-insensitive duplicate check
            var exists = await _context.Categories.AnyAsync(c => c.Name.ToLower() == category.Name.ToLower());
            if (exists)
            {
                return BadRequest(new { Message = $"Category '{category.Name}' already exists." });
            }

            _context.Categories.Add(category);
            await _context.SaveChangesAsync();
            return CreatedAtAction(nameof(Get), new { id = category.Id }, category);
        }
        catch (Exception ex)
        {
            Serilog.Log.Error(ex, "Error creating category");
            return StatusCode(500, new { Message = "Failed to create category. Database connection issue.", Detail = ex.Message });
        }
    }

    [HttpPut("{id}")]
    [Authorize]
    public async Task<IActionResult> Update(Guid id, [FromBody] Category category)
    {
        var existing = await _context.Categories.FindAsync(id);
        if (existing == null) return NotFound();

        existing.Name = category.Name;
        await _context.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("{id}")]
    [Authorize]
    public async Task<IActionResult> Delete(Guid id)
    {
        var category = await _context.Categories.FindAsync(id);
        if (category == null) return NotFound();

        // Check if usage
        var isUsed = await _context.Articles.AnyAsync(a => a.Category == category.Name);
        if (isUsed)
        {
            return BadRequest(new { Message = "Category is in use by one or more articles. Change the articles first." });
        }

        _context.Categories.Remove(category);
        await _context.SaveChangesAsync();
        return NoContent();
    }
}
