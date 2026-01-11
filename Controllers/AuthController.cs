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
    private readonly IConfiguration _configuration;
    private readonly IHttpClientFactory _httpClientFactory;

    public AuthController(UserManager<User> userManager, SignInManager<User> signInManager, IConfiguration configuration, IHttpClientFactory httpClientFactory)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _configuration = configuration;
        _httpClientFactory = httpClientFactory;
    }

    public class LoginRequest
    {
        public string Username { get; set; }
        public string Password { get; set; }
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


        var isPasswordValid = await _userManager.CheckPasswordAsync(user, request.Password);

        if (!isPasswordValid)
            return Unauthorized();

        var roles = await _userManager.GetRolesAsync(user);
        var role = roles.FirstOrDefault() ?? "Viewer";

        var token = GenerateJwtToken(user, role);

        return Ok(new
        {
            token,
            role
        });
    }

    public class GoogleLoginDto
    {
        public string Token { get; set; }
    }

    [HttpPost("google")]
    public async Task<IActionResult> GoogleLogin([FromBody] GoogleLoginDto model)
    {
        try
        {
            var googleClientId = _configuration["GoogleAuth:ClientId"];
            var payload = await Google.Apis.Auth.GoogleJsonWebSignature.ValidateAsync(model.Token, new Google.Apis.Auth.GoogleJsonWebSignature.ValidationSettings()
            {
                Audience = new[] { googleClientId }
            });

            var user = await _userManager.FindByEmailAsync(payload.Email);
            
            if (user == null)
            {
                // Create new user
                user = new User
                {
                    UserName = payload.Email, // ensure username is email for google users
                    Email = payload.Email,
                    FullName = payload.Name,
                    ProfileImage = payload.Picture,
                    EmailConfirmed = true 
                };
                
                var result = await _userManager.CreateAsync(user);
                if (!result.Succeeded) return BadRequest(result.Errors);
                
                // Assign "Viewer" role explicitly
                await _userManager.AddToRoleAsync(user, "Viewer");
            }
            else
            {
                // Refresh name and picture from Google on every login to keep it updated
                user.FullName = payload.Name;
                user.ProfileImage = payload.Picture;
                await _userManager.UpdateAsync(user);

                // Ensure existing Google users are also treated as Viewers if not already set (safety check)
                var currentRoles = await _userManager.GetRolesAsync(user);
                if (!currentRoles.Contains("Viewer") && !currentRoles.Contains("Admin") && !currentRoles.Contains("Editor"))
                {
                    await _userManager.AddToRoleAsync(user, "Viewer");
                }
            }

            var roles = await _userManager.GetRolesAsync(user);
            var role = roles.FirstOrDefault() ?? "Viewer";
            var token = GenerateJwtToken(user, role);
            return Ok(new { token, role });

        }
        catch (Exception ex)
        {
            // Log ex
            return BadRequest($"Google Login Failed: {ex.Message}");
        }
    }

    public class FacebookLoginDto
    {
        public string Token { get; set; }
    }

    private class FacebookUserResponse
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Email { get; set; }
        public FacebookPicture Picture { get; set; }
    }

    private class FacebookPicture
    {
        public FacebookPictureData Data { get; set; }
    }

    private class FacebookPictureData
    {
        public string Url { get; set; }
    }

    [HttpPost("facebook")]
    public async Task<IActionResult> FacebookLogin([FromBody] FacebookLoginDto model)
    {
        try
        {
            var client = _httpClientFactory.CreateClient();
            var response = await client.GetAsync($"https://graph.facebook.com/me?fields=id,name,email,picture&access_token={model.Token}");

            if (!response.IsSuccessStatusCode)
            {
                return BadRequest("Failed to verify Facebook token.");
            }

            var json = await response.Content.ReadAsStringAsync();
            var fbUser = System.Text.Json.JsonSerializer.Deserialize<FacebookUserResponse>(json, new System.Text.Json.JsonSerializerOptions 
            { 
                PropertyNameCaseInsensitive = true 
            });

            if (fbUser == null || (string.IsNullOrEmpty(fbUser.Email) && string.IsNullOrEmpty(fbUser.Id)))
            {
                return BadRequest("Invalid Facebook user data.");
            }

            // If email is missing (FB doesn't always provide it), use ID@facebook.com
            var email = fbUser.Email ?? $"{fbUser.Id}@facebook.com";
            var user = await _userManager.FindByEmailAsync(email);

            if (user == null)
            {
                user = new User
                {
                    UserName = email,
                    Email = email,
                    FullName = fbUser.Name,
                    ProfileImage = fbUser.Picture?.Data?.Url,
                    CreatedAt = DateTime.UtcNow,
                    EmailConfirmed = true
                };

                var createResult = await _userManager.CreateAsync(user);
                if (!createResult.Succeeded) return BadRequest(createResult.Errors);

                await _userManager.AddToRoleAsync(user, "Viewer");
            }
            else
            {
                // Sync latest name/photo
                user.FullName = fbUser.Name;
                user.ProfileImage = fbUser.Picture?.Data?.Url;
                await _userManager.UpdateAsync(user);

                // Ensure role is correct
                var currentRoles = await _userManager.GetRolesAsync(user);
                if (!currentRoles.Contains("Viewer") && !currentRoles.Contains("Admin") && !currentRoles.Contains("Editor"))
                {
                    await _userManager.AddToRoleAsync(user, "Viewer");
                }
            }

            var roles = await _userManager.GetRolesAsync(user);
            var role = roles.FirstOrDefault() ?? "Viewer";
            var token = GenerateJwtToken(user, role);

            return Ok(new { token, role });
        }
        catch (Exception ex)
        {
            Serilog.Log.Error(ex, "Facebook Login Execution Error");
            return BadRequest($"Facebook Login Failed: {ex.Message}");
        }
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



    private string GenerateJwtToken(User user, string role)
    {
        var tokenHandler = new JwtSecurityTokenHandler();
        var key = Encoding.ASCII.GetBytes(SecretKey);
        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(new[] 
            { 
                new Claim("unique_name", user.UserName ?? ""), 
                new Claim("role", role),
                new Claim("fullName", user.FullName ?? ""),
                new Claim("profileImage", !string.IsNullOrEmpty(user.ProfileImage) ? user.ProfileImage : $"https://ui-avatars.com/api/?name={Uri.EscapeDataString(user.FullName ?? user.UserName ?? "User")}&background=0D8ABC&color=fff&size=128")
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
