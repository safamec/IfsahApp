using System.ComponentModel.DataAnnotations;

namespace IfsahApp.Models;

public class Notification
{
    public int Id { get; set; }

    [Required]
    public int RecipientId { get; set; }
    public User Recipient { get; set; }

    [Required]
    public string EventType { get; set; } // e.g. "Submission", "Assignment", "ReviewComplete"

    [Required, MaxLength(500)]
    public string Message { get; set; }

    public string EmailAddress { get; set; } // Optional override

    public bool IsRead { get; set; } = false;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
