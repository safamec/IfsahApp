
using System.ComponentModel.DataAnnotations;

namespace IfsahApp.Core.Models;

public class DisclosureReview
{
    public int Id { get; set; }

    [Required]
    public int DisclosureId { get; set; }

    public Disclosure? Disclosure { get; set; } // nullable navigation property

    [Required]
    public int ReviewerId { get; set; } // Final review (Audit Manager)

    public User? Reviewer { get; set; } // nullable navigation property

    public string? ReviewSummary { get; set; } // optional

    public string? ReportFilePath { get; set; } // optional

    public DateTime ReviewedAt { get; set; } = DateTime.UtcNow;

    public string? Outcome { get; set; } // optional: Approved, Escalated, Closed
}