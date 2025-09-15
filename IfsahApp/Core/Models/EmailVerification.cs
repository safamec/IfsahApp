// IfsahApp.Core/Models/EmailVerification.cs
using System.ComponentModel.DataAnnotations;

namespace IfsahApp.Core.Models;

public class EmailVerification
{
    [Key] public Guid Id { get; set; } = Guid.NewGuid();

    public int UserId { get; set; }
    public User? User { get; set; }

    // store SHA256(token) – if DB leaks, token is still secret
    [MaxLength(64)]
    public string TokenHash { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAt { get; set; }        // e.g., now + 24h
    public DateTime? ConsumedAt { get; set; }      // one-time
    public int Attempts { get; set; } = 0;         // small defense

    // optional: categorize
    [MaxLength(32)]
    public string Purpose { get; set; } = "email_confirm";
}
