using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NewsPortal.API.Data;
using NewsPortal.API.Models;

namespace NewsPortal.API.Controllers;

[Route("api/[controller]")]
[ApiController]
public class UsersController : ControllerBase
{
    private readonly NewsContext _context;

    public UsersController(NewsContext context)
    {
        _context = context;
    }

    [HttpGet]
    [Authorize] // Ideally restrict to Role="Admin"
    public async Task<ActionResult<IEnumerable<User>>> GetUsers()
    {
        // Don't return passwords
        return await _context.Users.Select(u => new User { 
            Id = u.Id, 
            Username = u.Username, 
            Role = u.Role,
            Email = u.Email,
            FullName = u.FullName,
            CreatedAt = u.CreatedAt,
            PublicKey = u.PublicKey
        }).ToListAsync();
    }

    public class PublicKeyRequest
    {
        public string PublicKey { get; set; } = string.Empty;
    }

    [HttpPost("key")]
    [Authorize]
    public async Task<IActionResult> UpdatePublicKey([FromBody] PublicKeyRequest request)
    {
        var username = User.Identity?.Name;
        if (string.IsNullOrEmpty(username)) return Unauthorized();

        var user = await _context.Users.FirstOrDefaultAsync(u => u.Username == username);
        if (user == null) return NotFound();

        user.PublicKey = request.PublicKey;
        await _context.SaveChangesAsync();
        return Ok();
    }

    public class CreateUserDto
    {
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string Role { get; set; } = "Editor";
        public string Email { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
    }

    [HttpPost]
    [Authorize]
    public async Task<ActionResult<User>> CreateUser(CreateUserDto request)
    {
        if (await _context.Users.AnyAsync(u => u.Username == request.Username))
        {
            return BadRequest("Username already exists");
        }

        var user = new User
        {
            Username = request.Username,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password), // Use Hash!
            Role = request.Role,
            Email = request.Email,
            FullName = request.FullName,
            CreatedAt = DateTime.UtcNow
        };

        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        return CreatedAtAction(nameof(GetUsers), new { id = user.Id }, user);
    }

    [HttpDelete("{id}")]
    [Authorize]
    public async Task<IActionResult> DeleteUser(int id)
    {
        var user = await _context.Users.FindAsync(id);
        if (user == null) return NotFound();

        // Prevent deleting the last admin or "self" if strict, but let's just create basic
        if (user.Username.ToLower() == "admin") return BadRequest("Cannot delete root admin");

        _context.Users.Remove(user);
        await _context.SaveChangesAsync();

        return NoContent();
    }
    public class UpdateUserRequest
    {
        public string Username { get; set; }
        public string Role { get; set; }
        public string? NewPassword { get; set; }
        public string? Email { get; set; }
        public string? FullName { get; set; }
    }

    [HttpPut("{id}")]
    [Authorize]
    public async Task<IActionResult> UpdateUser(int id, [FromBody] UpdateUserRequest request)
    {
        var user = await _context.Users.FindAsync(id);
        if (user == null) return NotFound();

        // Check if username is taken by ANOTHER user
        if (await _context.Users.AnyAsync(u => u.Username == request.Username && u.Id != id))
        {
            return BadRequest("Username already taken");
        }

        user.Username = request.Username;
        user.Role = request.Role;
        user.Email = request.Email ?? user.Email;
        user.FullName = request.FullName ?? user.FullName;

        if (!string.IsNullOrEmpty(request.NewPassword))
        {
            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
        }

        await _context.SaveChangesAsync();
        return Ok(new { id = user.Id, username = user.Username, role = user.Role, email = user.Email, fullName = user.FullName });
    }
}
