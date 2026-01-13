using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NewsPortal.API.Data;
using NewsPortal.API.Models;
using Microsoft.AspNetCore.Identity;

namespace NewsPortal.API.Controllers;

[Route("api/[controller]")]
[ApiController]
public class UsersController : ControllerBase
{
    private readonly UserManager<User> _userManager;

    public UsersController(UserManager<User> userManager)
    {
        _userManager = userManager;
    }

    public class UserDto
    {
        public Guid Id { get; set; }
        public string Username { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public string? Email { get; set; }
        public string? FullName { get; set; }
        public DateTime CreatedAt { get; set; }
        public string PublicKey { get; set; } = string.Empty;
        public string? ProfileImage { get; set; }
    }

    [HttpGet]
    [Authorize]
    public async Task<ActionResult<IEnumerable<UserDto>>> GetUsers([FromQuery] bool employees = false)
    {
        var users = await _userManager.Users.ToListAsync();
        var userDtos = new List<UserDto>();

        foreach (var user in users)
        {
            var roles = await _userManager.GetRolesAsync(user);
            var role = roles.FirstOrDefault() ?? "Viewer";

            if (employees)
            {
                if (role != "Admin" && role != "Editor") continue;
            }

            userDtos.Add(new UserDto
            {
                Id = user.Id,
                Username = user.UserName ?? "",
                Role = role,
                Email = user.Email,
                FullName = user.FullName,
                CreatedAt = user.CreatedAt,
                PublicKey = user.PublicKey,
                ProfileImage = !string.IsNullOrEmpty(user.ProfileImage) ? user.ProfileImage : $"https://ui-avatars.com/api/?name={Uri.EscapeDataString(user.FullName ?? user.UserName ?? "User")}&background=0D8ABC&color=fff&size=128"
            });
        }

        return Ok(userDtos);
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

        var user = await _userManager.FindByNameAsync(username);
        if (user == null) return NotFound();

        user.PublicKey = request.PublicKey;
        var result = await _userManager.UpdateAsync(user);
        
        if (result.Succeeded) return Ok();
        return BadRequest(result.Errors);
    }

    public class CreateUserDto
    {
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string Role { get; set; } = "Editor";
        public string Email { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string? ProfileImage { get; set; }
    }

    [HttpPost]
    [Authorize]
    public async Task<ActionResult<UserDto>> CreateUser(CreateUserDto request)
    {
        if (await _userManager.FindByNameAsync(request.Username) != null)
        {
            return BadRequest("Username already exists");
        }

        var user = new User
        {
            UserName = request.Username,
            Email = request.Email,
            FullName = request.FullName,
            ProfileImage = request.ProfileImage,
            CreatedAt = DateTime.UtcNow,
            EmailConfirmed = true
        };


        var result = await _userManager.CreateAsync(user, request.Password);

        if (!result.Succeeded)
        {
            return BadRequest(result.Errors);
        }


        if (!string.IsNullOrEmpty(request.Role))
        {
            await _userManager.AddToRoleAsync(user, request.Role);
        }

        return CreatedAtAction(nameof(GetUsers), new { id = user.Id }, new UserDto { Id = user.Id, Username = user.UserName, Role = request.Role });
    }

    [HttpDelete("{id}")]
    [Authorize]
    public async Task<IActionResult> DeleteUser(Guid id)
    {
        var user = await _userManager.FindByIdAsync(id.ToString());
        if (user == null) return NotFound();


        if (user.UserName?.ToLower() == "admin") return BadRequest("Cannot delete root admin");

        var result = await _userManager.DeleteAsync(user);
        if (result.Succeeded) return NoContent();
        
        return BadRequest(result.Errors);
    }

    public class UpdateUserRequest
    {
        public string Username { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public string? NewPassword { get; set; }
        public string? Email { get; set; }
        public string? FullName { get; set; }
        public string? ProfileImage { get; set; }
    }

    [HttpPut("{id}")]
    [Authorize]
    public async Task<IActionResult> UpdateUser(Guid id, [FromBody] UpdateUserRequest request)
    {
        var user = await _userManager.FindByIdAsync(id.ToString());
        if (user == null) return NotFound();


        if (user.UserName?.ToLower() == "admin")
        {

             if (request.Username?.ToLower() != "admin")
             {
                 return BadRequest("Cannot change the username of the root admin user.");
             }
             

        }

        user.UserName = request.Username;
        user.Email = request.Email;
        user.FullName = request.FullName;
        user.ProfileImage = request.ProfileImage;


        var updateResult = await _userManager.UpdateAsync(user);
        if (!updateResult.Succeeded) return BadRequest(updateResult.Errors);


        var currentRoles = await _userManager.GetRolesAsync(user);
        

        if (user.UserName?.ToLower() == "admin" && request.Role != "Admin") 
        {
             return BadRequest("Cannot change the role of the root admin user.");
        }

        if (!currentRoles.Contains(request.Role))
        {
            await _userManager.RemoveFromRolesAsync(user, currentRoles);
            await _userManager.AddToRoleAsync(user, request.Role);
        }


        if (!string.IsNullOrEmpty(request.NewPassword))
        {
            var token = await _userManager.GeneratePasswordResetTokenAsync(user);
            var pwdResult = await _userManager.ResetPasswordAsync(user, token, request.NewPassword);
            if (!pwdResult.Succeeded) return BadRequest(pwdResult.Errors);
        }

        return Ok(new { id = user.Id, username = user.UserName, role = request.Role });
    }
}
