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
        options.UseNpgsql(connectionString, npgsqlOptions =>
        {
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

    // Seed initial data (Removed - Manual SQL preferred)
    // To seed successfully, run the provided SQL script in your database directly.

    // Force ViewCount column check (Optional safety)
    using (var scope = app.Services.CreateScope())
    {
         var context = scope.ServiceProvider.GetRequiredService<NewsContext>();
         try 
         {
             // Ensure DB is created (Still useful for first run)
             context.Database.EnsureCreated();
         }
         catch(Exception ex) 
         {
             Log.Error(ex, "Error ensuring database created.");
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
