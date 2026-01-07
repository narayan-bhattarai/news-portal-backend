namespace NewsPortal.API.Models;

public class User
{
    public int Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty; // Storing hash, not plain text ideally
    public string Role { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? FullName { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string PublicKey { get; set; } = string.Empty; // For E2EE
    public string PrivateKey { get; set; } = string.Empty; // Storing for sync across devices
}
