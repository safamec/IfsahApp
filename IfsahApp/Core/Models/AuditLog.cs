using System.ComponentModel.DataAnnotations;

namespace IfsahApp.Core.Models;

public class AuditLog
{
    public int Id { get; set; }

    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    [Required]
    public string Action { get; set; } = string.Empty; // initialized to avoid null

    [Required]
    public int PerformedById { get; set; }

    public User? PerformedBy { get; set; } // nullable navigation property

    public string? IPAddress { get; set; } // optional, nullable

    public string? Context { get; set; } // optional, nullable (e.g., "DisclosureSubmission", "Assignment")
}

