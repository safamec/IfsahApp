
using System.ComponentModel.DataAnnotations;

namespace IfsahApp.Models;

public class DisclosureReview
{
    public int Id { get; set; }

    [Required]
    public int DisclosureId { get; set; }
    public Disclosure Disclosure { get; set; }

    [Required]
    public int ReviewerId { get; set; } // Final review (Audit Manager)
    public User Reviewer { get; set; }

    public string ReviewSummary { get; set; }

    public string ReportFilePath { get; set; }

    public DateTime ReviewedAt { get; set; } = DateTime.UtcNow;

    public string Outcome { get; set; } // Approved, Escalated, Closed
}
