using Microsoft.EntityFrameworkCore;
using NewsPortal.API.Data;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using Serilog;
using NewsPortal.API.Middleware;

// Configure Serilog
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .WriteTo.File("logs/log-.txt", rollingInterval: RollingInterval.Day)
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

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
                maxRetryCount: 5,
                maxRetryDelay: TimeSpan.FromSeconds(10),
                errorCodesToAdd: null);
            npgsqlOptions.CommandTimeout(60); // Increase to 60s for Render free tier
        }));

    // JWT Authentication - Prioritize Environment Variable
    var jwtKey = Environment.GetEnvironmentVariable("JWT_SECRET") 
                 ?? "SuperSecretKeyForNewsPortalDemo123!"; // Default for local dev only
    
    var key = Encoding.ASCII.GetBytes(jwtKey);
    
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
            ValidateIssuer = false,
            ValidateAudience = false,
            ClockSkew = TimeSpan.Zero
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

    // Database initialization (stateless on startup)
    // Note: Migrations should be handled via 'dotnet ef database update' locally 
    // or SQL exports for Supabase. No EnsureCreated() or schema mutations here.

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
