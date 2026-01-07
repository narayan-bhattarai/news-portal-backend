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

    // Configure PostgreSQL
    builder.Services.AddDbContext<NewsContext>(options =>
        options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

    // Configure JWT Authentication
    var key = Encoding.ASCII.GetBytes("SuperSecretKeyForNewsPortalDemo123!"); // Must match controller
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
            ValidateAudience = false
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

    // Global Exception Handling Middleware
    app.UseMiddleware<ExceptionHandlingMiddleware>();

    // Ensure Database is Created
    //using (var scope = app.Services.CreateScope())
    //{
    //    var context = scope.ServiceProvider.GetRequiredService<NewsContext>();
    //    var config = scope.ServiceProvider.GetRequiredService<IConfiguration>();
    //   // context.Database.EnsureCreated();

    //    // Manual Schema Update to support Email and FullName
    //    try
    //    {
    //        context.Database.ExecuteSqlRaw(@"
    //            DO $$ 
    //            BEGIN 
    //              IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name='Users' AND column_name='Email') THEN 
    //                ALTER TABLE ""Users"" ADD COLUMN ""Email"" text; 
    //              END IF;
    //              IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name='Users' AND column_name='FullName') THEN 
    //                ALTER TABLE ""Users"" ADD COLUMN ""FullName"" text; 
    //              END IF;
    //              IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name='Articles' AND column_name='PublishedAt') THEN 
    //                ALTER TABLE ""Articles"" ADD COLUMN ""PublishedAt"" timestamp with time zone DEFAULT now(); 
    //              END IF;
    //              IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name='ContactSubmissions' AND column_name='Phone') THEN 
    //                ALTER TABLE ""ContactSubmissions"" ADD COLUMN ""Phone"" text DEFAULT ''; 
    //              END IF;
    //              IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name='Users' AND column_name='PrivateKey') THEN 
    //                ALTER TABLE ""Users"" ADD COLUMN ""PrivateKey"" text DEFAULT ''; 
    //              END IF;
    //            END $$;");
    //    }
    //    catch (Exception ex)
    //    {
    //         Log.Error(ex, "Failed to apply manual schema updates");
    //    }

    //    // Explicitly seed Admin from Config if table is empty or missing admin
    //    var existingAdmin = context.Users.FirstOrDefault(u => u.Role == "Admin");
    //    var initialAdmin = config.GetSection("InitialAdminCredentials");

    //    if (existingAdmin == null)
    //    {
    //        if (initialAdmin.Exists())
    //        {
    //            context.Users.Add(new NewsPortal.API.Models.User
    //            {
    //                 Username = initialAdmin["Username"],
    //                 PasswordHash = BCrypt.Net.BCrypt.HashPassword(initialAdmin["Password"]), 
    //                 Role = "Admin",
    //                 FullName = initialAdmin["FullName"]
    //            });
    //        }
    //    }
    //    else if (initialAdmin.Exists() && initialAdmin["Username"] == existingAdmin.Username && existingAdmin.FullName != initialAdmin["FullName"])
    //    {
    //         existingAdmin.FullName = initialAdmin["FullName"];
    //         // Note: The original code uses context.SaveChanges() at the end of the scope.
    //         // If userManager was intended, it would need to be injected and the method made async.
    //         // For now, relying on context.SaveChanges() to persist the change.
    //         Console.WriteLine($"Updated existing admin user's FullName to '{existingAdmin.FullName}'");
    //    }

    //    context.SaveChanges();
    //}

    if (app.Environment.IsDevelopment() || app.Environment.IsProduction())
    {
        app.UseSwagger();
        app.UseSwaggerUI();
    }
    // app.UseHttpsRedirection();

    app.UseCors("AllowFrontend");

    app.UseStaticFiles(); // Enable static file serving (for uploads)

    app.UseAuthentication(); // Enable Auth
    app.UseAuthorization();

    app.MapControllers();
    app.MapHub<NewsPortal.API.Hubs.ChatHub>("/chatHub");

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
