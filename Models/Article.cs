namespace NewsPortal.API.Models;

public class Article
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Category { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Excerpt { get; set; } = string.Empty;
    public string Author { get; set; } = string.Empty;
    public string ImageUrl { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty; // Added Content field
    public bool IsTrending { get; set; }
    public bool IsEditorsPick { get; set; }
    public bool IsFeatured { get; set; }
    public int ViewCount { get; set; } = 0; // Added ViewCount
    public DateTime PublishedAt { get; set; } = DateTime.UtcNow;
}
