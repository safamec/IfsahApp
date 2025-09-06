using System.ComponentModel.DataAnnotations;

namespace IfsahApp.Core.Models;

public class Notification
{
    public int Id { get; set; }

    [Required]
    public int RecipientId { get; set; }

    public User? Recipient { get; set; } // nullable nav property

    [Required]
    public string EventType { get; set; } = string.Empty; // initialized to avoid null

    [Required, MaxLength(500)]
    public string Message { get; set; } = string.Empty; // initialized to avoid null

    public string? EmailAddress { get; set; } // optional

    public bool IsRead { get; set; } = false;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}