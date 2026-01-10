using Microsoft.EntityFrameworkCore;
using NewsPortal.API.Data;
using NewsPortal.API.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using Serilog;
using NewsPortal.API.Middleware;
using System.Security.Claims;

// Configure Serilog
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .WriteTo.File("logs/log-.txt", rollingInterval: RollingInterval.Day)
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);
var port = Environment.GetEnvironmentVariable("PORT") ?? "8080";
builder.WebHost.UseUrls($"http://0.0.0.0:{port}");
    // Use Serilog
    builder.Host.UseSerilog((ctx, lc) => lc
        .ReadFrom.Configuration(ctx.Configuration));

    // Add services to the container.
    builder.Services.AddControllers();
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen();
    builder.Services.AddSignalR();

    // Database Connection String - Prioritize Environment Variable (for Supabase/Production)
    var connectionString = Environment.GetEnvironmentVariable("DATABASE_URL") 
                         ?? builder.Configuration.GetConnectionString("DefaultConnection");

    builder.Services.AddDbContext<NewsContext>(options =>
        options.UseNpgsql(connectionString, npgsqlOptions => {
            npgsqlOptions.EnableRetryOnFailure(
                maxRetryCount: 10,
                maxRetryDelay: TimeSpan.FromSeconds(30),
                errorCodesToAdd: null);
            npgsqlOptions.CommandTimeout(120); // Increase to 120s to match logs (122s delay)
        }));

    // Add Identity (BEFORE Authentication)
    builder.Services.AddIdentity<User, IdentityRole<Guid>>(options =>
    {
        options.User.RequireUniqueEmail = false; // allow null emails as per current model
        // Password settings
        options.Password.RequireDigit = false;
        options.Password.RequireLowercase = false;
        options.Password.RequireNonAlphanumeric = false;
        options.Password.RequireUppercase = false;
        options.Password.RequiredLength = 6;
    })
    .AddEntityFrameworkStores<NewsContext>()
    .AddDefaultTokenProviders();

    // JWT Authentication - Prioritize Environment Variable
    var jwtKey = Environment.GetEnvironmentVariable("JWT_SECRET") 
                 ?? "SuperSecretKeyForNewsPortalDemo123!"; // Default for local dev only
    
    var key = Encoding.ASCII.GetBytes(jwtKey);
    
    // Config Auth to use JWT by default (override Identity cookies)
    builder.Services.AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(options =>
    {
        options.RequireHttpsMetadata = false;
        options.SaveToken = true;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(key),
            ValidateIssuer = false, // Identity doesn't check issuer by default unless configured
            ValidateAudience = false,
            ClockSkew = TimeSpan.Zero,
            NameClaimType = ClaimTypes.Name,
            RoleClaimType = ClaimTypes.Role
        };

        // Handle SignalR tokens from Query String
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];
                var path = context.HttpContext.Request.Path;
                if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/chatHub"))
                {
                    context.Token = accessToken;
                }
                return Task.CompletedTask;
            }
        };
    });

    // Configure CORS
    var allowedOrigins = builder.Configuration.GetSection("AllowedOrigins").Get<string[]>() ?? Array.Empty<string>();

    builder.Services.AddCors(options =>
    {
        options.AddPolicy("AllowFrontend",
            policy =>
            {
                policy.WithOrigins(allowedOrigins)
                      .AllowAnyHeader()
                      .AllowAnyMethod()
                      .AllowCredentials();
            });
    });

    var app = builder.Build();

    // Global Exception Handling
    app.UseMiddleware<ExceptionHandlingMiddleware>();

    // Enable Swagger for all environments (useful for testing on Render)
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "NewsPortal.API v1");
        c.RoutePrefix = "swagger"; // This ensures it stays at /swagger
    });

    app.UseStaticFiles(); // Served uploads, etc.
    app.UseRouting();

    app.UseCors("AllowFrontend");

    // Order: Authentication -> Authorization -> Endpoints
    app.UseAuthentication();
    app.UseAuthorization();

    app.MapControllers();
    app.MapHub<NewsPortal.API.Hubs.ChatHub>("/chatHub");

    // Seed initial data (Admin user, Categories, etc.)
    using (var scope = app.Services.CreateScope())
    {
        var services = scope.ServiceProvider;
        try
        {
            var context = services.GetRequiredService<NewsContext>();
            var userManager = services.GetRequiredService<UserManager<User>>();
            var roleManager = services.GetRequiredService<RoleManager<IdentityRole<Guid>>>(); // Inject RoleManager

            // 1. Seed Roles
            string[] roles = { "Admin", "Editor", "User" };
            foreach (var role in roles)
            {
                if (!await roleManager.RoleExistsAsync(role))
                {
                    await roleManager.CreateAsync(new IdentityRole<Guid>(role));
                }
            }

            // 2. Seed Admin User
            if (await userManager.FindByNameAsync("Admin") == null)
            {
                Log.Information("Seeding default admin user...");
                var adminUser = new User
                {
                    UserName = "Admin",
                    Email = "admin@theeverestedit.com",
                    FullName = "Narine Bhattarai",
                    CreatedAt = DateTime.UtcNow,
                    EmailConfirmed = true
                };

                var result = await userManager.CreateAsync(adminUser, "Admin@5175");
                if (result.Succeeded)
                {
                     // Assign Admin Role
                     await userManager.AddToRoleAsync(adminUser, "Admin");
                }
                else
                {
                    foreach (var error in result.Errors)
                    {
                        Log.Error("Error creating admin user: {Error}", error.Description);
                    }
                }
            }

            if (!context.Categories.Any())
            {
                Log.Information("Seeding default categories...");
                context.Categories.AddRange(
                    new NewsPortal.API.Models.Category { Id = Guid.NewGuid(), Name = "World" },
                    new NewsPortal.API.Models.Category { Id = Guid.NewGuid(), Name = "Business" },
                    new NewsPortal.API.Models.Category { Id = Guid.NewGuid(), Name = "Technology" },
                    new NewsPortal.API.Models.Category { Id = Guid.NewGuid(), Name = "Sports" },
                    new NewsPortal.API.Models.Category { Id = Guid.NewGuid(), Name = "Entertainment" }
                );
                context.SaveChanges();
            }

            // AUTO-PATCH: Ensure ViewCount column exists
            try 
            {
                context.Database.ExecuteSqlRaw("ALTER TABLE \"Articles\" ADD COLUMN IF NOT EXISTS \"ViewCount\" integer NOT NULL DEFAULT 0;");
                Log.Information("Database patched: ViewCount column verified.");
            }
            catch (Exception) { /* Ignored if already exists or other minor issue */ }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "An error occurred during database seeding.");
        }
    }

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}
