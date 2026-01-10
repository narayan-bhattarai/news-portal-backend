using Microsoft.AspNetCore.Identity;

namespace NewsPortal.API.Models;

public class User : IdentityUser<Guid>
{
    // Inherited from IdentityUser<Guid>:
    // Guid Id
    // string UserName
    // string Email
    // string PasswordHash
    // string PhoneNumber
    // ...

    // Role is now handled by AspNetUserRoles table (Identity)
    // public string Role { get; set; } = string.Empty; 

    public string? FullName { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string PublicKey { get; set; } = string.Empty; 
    public string PrivateKey { get; set; } = string.Empty; 
    public string? ProfileImage { get; set; }
}
