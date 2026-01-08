namespace NewsPortal.API.Models;

public class Article
{
    public int Id { get; set; }
    public string Category { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Excerpt { get; set; } = string.Empty;
    public string Author { get; set; } = string.Empty;
    public string ImageUrl { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty; // Added Content field
    public bool IsTrending { get; set; }
    public DateTime PublishedAt { get; set; } = DateTime.UtcNow;
}
