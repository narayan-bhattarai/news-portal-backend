using Microsoft.AspNetCore.Identity;

namespace NewsPortal.API.Models;

public class User : IdentityUser<Guid>
{
    public string? FullName { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string PublicKey { get; set; } = string.Empty; 
    public string PrivateKey { get; set; } = string.Empty; 
    public string? ProfileImage { get; set; }
}
