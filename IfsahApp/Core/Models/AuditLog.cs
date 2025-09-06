using System.ComponentModel.DataAnnotations;

namespace IfsahApp.Models;

public class AuditLog
{
    public int Id { get; set; }

    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    [Required]
    public string Action { get; set; }

    [Required]
    public int PerformedById { get; set; }
    public User PerformedBy { get; set; }

    public string IPAddress { get; set; }

    public string Context { get; set; } // e.g. "DisclosureSubmission", "Assignment", etc.
}
