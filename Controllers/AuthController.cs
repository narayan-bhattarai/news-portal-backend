using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
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
    private readonly UserManager<User> _userManager;
    private readonly SignInManager<User> _signInManager;

    public AuthController(UserManager<User> userManager, SignInManager<User> signInManager)
    {
        _userManager = userManager;
        _signInManager = signInManager;
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Username) || 
            string.IsNullOrWhiteSpace(request.Password))
        {
            return Unauthorized();
        }

        var user = await _userManager.FindByNameAsync(request.Username);

        if (user == null)
            return Unauthorized();

        // Use Identity's password check (handles hashing automatically)
        var isPasswordValid = await _userManager.CheckPasswordAsync(user, request.Password);

        // Fallback: Check if password is stored as Plain Text (Migration Logic)
        if (!isPasswordValid && user.PasswordHash == request.Password)
        {
            // Detected plain text password. Hash it and update.
            var resetToken = await _userManager.GeneratePasswordResetTokenAsync(user);
            var result = await _userManager.ResetPasswordAsync(user, resetToken, request.Password);
            
            if (result.Succeeded)
            {
                isPasswordValid = true;
            }
        }

        if (!isPasswordValid)
            return Unauthorized();

        var roles = await _userManager.GetRolesAsync(user);
        var role = roles.FirstOrDefault() ?? "User"; // Default to User if no role found

        var token = GenerateJwtToken(user, role);

        return Ok(new
        {
            token,
            role
        });
    }

    public class RegisterRequest
    {
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request)
    {
        if (await _userManager.FindByNameAsync(request.Username) != null)
        {
            return BadRequest("Username already exists");
        }

        var newUser = new User
        {
            UserName = request.Username,
            CreatedAt = DateTime.UtcNow,
            Email = request.Username + "@example.com" 
        };
        
        var result = await _userManager.CreateAsync(newUser, request.Password);

        if (result.Succeeded)
        {
            if (!string.IsNullOrEmpty(request.Role))
            {
                await _userManager.AddToRoleAsync(newUser, request.Role);
            }
            return Ok(new { message = "User created successfully" });
        }

        return BadRequest(result.Errors);
    }

    // ... (Keys endpoints remain same)

    private string GenerateJwtToken(User user, string role)
    {
        var tokenHandler = new JwtSecurityTokenHandler();
        var key = Encoding.ASCII.GetBytes(SecretKey);
        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(new[] 
            { 
                new Claim(ClaimTypes.Name, user.UserName ?? ""), 
                new Claim(ClaimTypes.Role, role),
                new Claim("fullName", user.FullName ?? "")
            }),
            Expires = DateTime.UtcNow.AddDays(7),
            SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
        };
        var token = tokenHandler.CreateToken(tokenDescriptor);
        return tokenHandler.WriteToken(token);
    }

    [HttpGet("keys")]
    [Authorize]
    public async Task<IActionResult> GetKeys()
    {
        var username = User.Identity?.Name;
        if (string.IsNullOrEmpty(username)) return Unauthorized();

        var user = await _userManager.FindByNameAsync(username);
        if (user == null) return NotFound("User not found");

        return Ok(new { publicKey = user.PublicKey, privateKey = user.PrivateKey });
    }

    public class KeyUpdateRequest
    {
        public string PublicKey { get; set; } = string.Empty;
        public string PrivateKey { get; set; } = string.Empty;
    }

    [HttpPost("keys")]
    [Authorize]
    public async Task<IActionResult> UpdateKeys([FromBody] KeyUpdateRequest request)
    {
        var username = User.Identity?.Name;
        if (string.IsNullOrEmpty(username)) return Unauthorized();

        var user = await _userManager.FindByNameAsync(username);
        if (user == null) return NotFound("User not found");

        user.PublicKey = request.PublicKey;
        user.PrivateKey = request.PrivateKey;
        
        var result = await _userManager.UpdateAsync(user);

        if (result.Succeeded)
        {
             return Ok(new { message = "Keys updated successfully" });
        }
        return BadRequest("Failed to update keys");
    }


}
