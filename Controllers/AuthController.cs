using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using NewsPortal.API.Data;
using NewsPortal.API.Models;
using System.Security.Claims;
using System.IdentityModel.Tokens.Jwt;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace NewsPortal.API.Controllers;

[Route("api/[controller]")]
[ApiController]
public class AuthController : ControllerBase
{
    private const string SecretKey = "SuperSecretKeyForNewsPortalDemo123!";
    private readonly NewsContext _context;

    public AuthController(NewsContext context)
    {
        _context = context;
    }

    [HttpPost("login")]
    public IActionResult Login([FromBody] LoginRequest request)
    {
        // Fetch user by username ONLY first
        var user = _context.Users.FirstOrDefault(u => u.Username == request.Username);

        if (user != null)
        {
            // Verify Password using BCrypt
            bool validPassword = false;
            try
            {
                validPassword = BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash);
            }
            catch 
            {
                // Fallback for legacy plain-text passwords (temporary migration support)
                if (user.PasswordHash == request.Password)
                {
                    // Auto-migrate to hash?
                    user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password);
                    _context.SaveChanges();
                    validPassword = true;
                }
            }

            if (validPassword)
            {
                var token = GenerateJwtToken(user);
                return Ok(new { token, role = user.Role });
            }
        }

        return Unauthorized();
    }

    [HttpPost("register")]
    public IActionResult Register([FromBody] RegisterRequest request)
    {
        if (_context.Users.Any(u => u.Username == request.Username))
        {
            return BadRequest("Username already exists");
        }

        var newUser = new User
        {
            Username = request.Username,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password), // Secure Hash
            Role = request.Role,
            CreatedAt = DateTime.UtcNow
        };
        
        _context.Users.Add(newUser);
        _context.SaveChanges();

        return Ok(new { message = "User created successfully" });
    }

    [HttpGet("keys")]
    [Authorize]
    public IActionResult GetKeys()
    {
        var username = User.Identity?.Name;
        if (string.IsNullOrEmpty(username)) return Unauthorized();

        var user = _context.Users.FirstOrDefault(u => u.Username == username);
        if (user == null) return NotFound("User not found");

        return Ok(new { publicKey = user.PublicKey, privateKey = user.PrivateKey });
    }

    [HttpPost("keys")]
    [Authorize]
    public IActionResult UpdateKeys([FromBody] KeyUpdateRequest request)
    {
        var username = User.Identity?.Name;
        if (string.IsNullOrEmpty(username)) return Unauthorized();

        var user = _context.Users.FirstOrDefault(u => u.Username == username);
        if (user == null) return NotFound("User not found");

        user.PublicKey = request.PublicKey;
        user.PrivateKey = request.PrivateKey;
        _context.SaveChanges();

        return Ok(new { message = "Keys updated successfully" });
    }

    public class KeyUpdateRequest
    {
        public string PublicKey { get; set; } = string.Empty;
        public string PrivateKey { get; set; } = string.Empty;
    }

    public class RegisterRequest
    {
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
    }

    private string GenerateJwtToken(User user)
    {
        var tokenHandler = new JwtSecurityTokenHandler();
        var key = Encoding.ASCII.GetBytes(SecretKey);
        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(new[] 
            { 
                new Claim(ClaimTypes.Name, user.Username),
                new Claim(ClaimTypes.Role, user.Role),
                new Claim("fullName", user.FullName ?? "")
            }),
            Expires = DateTime.UtcNow.AddDays(7),
            SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
        };
        var token = tokenHandler.CreateToken(tokenDescriptor);
        return tokenHandler.WriteToken(token);
    }
}
