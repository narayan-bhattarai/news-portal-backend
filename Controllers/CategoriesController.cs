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
            

            var categoryCounts = await _context.Articles
                .Where(a => a.CategoryId != null)
                .GroupBy(a => a.CategoryId)
                .Select(g => new { CategoryId = g.Key, Count = g.Count() })
                .ToListAsync();

            var result = categories.Select(c => new {
                c.Id,
                c.Name,
                ArticleCount = categoryCounts.FirstOrDefault(cc => cc.CategoryId == c.Id)?.Count ?? 0
            }).OrderByDescending(c => c.ArticleCount).ToList();

            return Ok(result);
        }
        catch (Exception ex)
        {
            Console.WriteLine("CRITICAL_ERROR_GET_CATEGORIES: " + ex.ToString());
            Serilog.Log.Error(ex, "Error fetching categories");
            return StatusCode(500, new { Message = "Failed to load categories.", Detail = ex.Message });
        }
    }

    [HttpPost]
    [Authorize(Roles = "Admin,Editor")]
    public async Task<IActionResult> Create([FromBody] Category category)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(category.Name))
            {
                return BadRequest(new { Message = "Category name is required." });
            }


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
    [Authorize(Roles = "Admin,Editor")]
    public async Task<IActionResult> Update(Guid id, [FromBody] Category category)
    {
        var existing = await _context.Categories.FindAsync(id);
        if (existing == null) return NotFound();

        existing.Name = category.Name;
        await _context.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = "Admin,Editor")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var category = await _context.Categories.FindAsync(id);
        if (category == null) return NotFound();


        var isUsed = await _context.Articles.AnyAsync(a => a.CategoryId == id);
        if (isUsed)
        {
            return BadRequest(new { Message = "Category is in use by one or more articles. Change the articles first." });
        }

        _context.Categories.Remove(category);
        await _context.SaveChangesAsync();
        return NoContent();
    }
}
