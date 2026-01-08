using Microsoft.EntityFrameworkCore;
using NewsPortal.API.Models;

namespace NewsPortal.API.Data;

public class NewsContext : DbContext
{
    public NewsContext(DbContextOptions<NewsContext> options) : base(options)
    {
    }

    public DbSet<Article> Articles { get; set; }
    public DbSet<Category> Categories { get; set; }
    public DbSet<PageContent> Pages { get; set; }
    public DbSet<ContactSubmission> ContactSubmissions { get; set; }
    public DbSet<User> Users { get; set; }
    public DbSet<ChatMessage> ChatMessages { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Uniqueness Constraints
        modelBuilder.Entity<Category>()
            .HasIndex(c => c.Name)
            .IsUnique();

        modelBuilder.Entity<User>()
            .HasIndex(u => u.Username)
            .IsUnique();

        // Performance Indexes
        modelBuilder.Entity<Article>()
            .HasIndex(a => a.Category);
        
        modelBuilder.Entity<Article>()
            .HasIndex(a => a.PublishedAt);
        
        modelBuilder.Entity<Article>()
            .HasIndex(a => a.IsTrending);

        modelBuilder.Entity<User>()
            .HasIndex(u => u.Role);

        // Seed initial Articles (minimal)
        modelBuilder.Entity<Article>().HasData(
            new Article
            {
                Id = 1,
                Category = "Technology",
                Title = "The EV Revolution: New Solid-State Batteries Promise 1000km Range",
                Excerpt = "Major breakthroughs in battery technology are set to eliminate range anxiety and accelerate the transition to electric mobility.",
                Author = "Sarah Chen",
                ImageUrl = "/images/tech-ev.png",
                IsTrending = true,
                PublishedAt = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc)
            }
        );

        modelBuilder.Entity<Category>().HasData(
            new Category { Id = 1, Name = "World" },
            new Category { Id = 2, Name = "Business" },
            new Category { Id = 3, Name = "Technology" },
            new Category { Id = 4, Name = "Sports" },
            new Category { Id = 5, Name = "Entertainment" }
        );

        modelBuilder.Entity<PageContent>().HasData(
            new PageContent 
            { 
                Slug = "about", 
                Title = "About Us", 
                Body = "Welcome to Khabar Manch...",
                LastUpdated = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc)
            },
            new PageContent 
            { 
                Slug = "careers", 
                Title = "Careers", 
                Body = "We are currently fully staffed...",
                LastUpdated = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc)
            }
        );
    }
}
