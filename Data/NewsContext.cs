using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using NewsPortal.API.Models;

namespace NewsPortal.API.Data;

public class NewsContext : IdentityDbContext<User, IdentityRole<Guid>, Guid>
{
    public NewsContext(DbContextOptions<NewsContext> options) : base(options)
    {
    }

    public DbSet<Article> Articles { get; set; }
    public DbSet<Category> Categories { get; set; }
    public DbSet<PageContent> Pages { get; set; }
    public DbSet<ContactSubmission> ContactSubmissions { get; set; }
    // Users is inherited
    public DbSet<ChatMessage> ChatMessages { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Map Identity tables to cleaner names (optional but good for this transition)
        modelBuilder.Entity<User>().ToTable("Users");
        modelBuilder.Entity<IdentityRole<Guid>>().ToTable("Roles");
        modelBuilder.Entity<IdentityUserClaim<Guid>>().ToTable("UserClaims");
        modelBuilder.Entity<IdentityUserRole<Guid>>().ToTable("UserRoles");
        modelBuilder.Entity<IdentityUserLogin<Guid>>().ToTable("UserLogins");
        modelBuilder.Entity<IdentityRoleClaim<Guid>>().ToTable("RoleClaims");
        modelBuilder.Entity<IdentityUserToken<Guid>>().ToTable("UserTokens");

        // Uniqueness Constraints
        modelBuilder.Entity<Category>()
            .HasIndex(c => c.Name)
            .IsUnique();

        // Identity handles Username uniqueness usually, but explicit map is fine
        
        // Performance Indexes
        modelBuilder.Entity<Article>()
            .HasIndex(a => a.Category);
        
        modelBuilder.Entity<Article>()
            .HasIndex(a => a.PublishedAt);
        
        modelBuilder.Entity<Article>()
            .HasIndex(a => a.IsTrending);



        // Seed initial Articles (minimal)
        modelBuilder.Entity<Article>().HasData(
            new Article
            {
                Id = Guid.Parse("d2b7d4b4-825c-4c4f-96a1-a48971481e28"),
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
            new Category { Id = Guid.Parse("1e9d1e4e-0e3a-4e8c-84d4-53965e69315b"), Name = "World" },
            new Category { Id = Guid.Parse("2620c58e-0cf2-4a7b-a48d-29007f9c8f61"), Name = "Business" },
            new Category { Id = Guid.Parse("3c93ee5e-6e84-4d8e-8a03-757041571d9d"), Name = "Technology" },
            new Category { Id = Guid.Parse("4a8f9f0a-605a-4b9a-9e1e-7f6a7d8c9e0f"), Name = "Sports" },
            new Category { Id = Guid.Parse("5b9e0f1a-b2c3-4d5e-6f7a-8b9c0d1e2f3a"), Name = "Entertainment" }
        );

        modelBuilder.Entity<PageContent>().HasData(
            new PageContent 
            { 
                Slug = "about", 
                Title = "About Us", 
                Body = "Welcome to The Everest Edit...",
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
