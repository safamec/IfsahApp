using System.ComponentModel.DataAnnotations;
using IfsahApp.Enums;

namespace IfsahApp.Models;

public class Disclosure
{
    public int Id { get; set; }

    [Required]
    public string DisclosureNumber { get; set; } // e.g., DISC-2025-0001

    [Required, MaxLength(300)]
    public string Description { get; set; }

    public DateTime? IncidentStartDate { get; set; }
    public DateTime? IncidentEndDate { get; set; }

    [MaxLength(100)]
    public string Location { get; set; }

    [Required]
    public DateTime SubmittedAt { get; set; } = DateTime.UtcNow;

    // Use enum instead of string
    public DisclosureStatus Status { get; set; } = DisclosureStatus.New;

    [Required]
    public int DisclosureTypeId { get; set; }

    public DisclosureType DisclosureType { get; set; }

    [Required]
    public int SubmittedById { get; set; }
    public User SubmittedBy { get; set; }

    // Strongly typed collections
    public ICollection<SuspectedPerson> SuspectedPeople { get; set; } = new List<SuspectedPerson>();
    public ICollection<RelatedPerson> RelatedPeople { get; set; } = new List<RelatedPerson>();
    public ICollection<DisclosureAttachment> Attachments { get; set; }
    public ICollection<DisclosureNote> Notes { get; set; }
    public ICollection<DisclosureAssignment> Assignments { get; set; }
    public DisclosureReview FinalReview { get; set; }

    public bool IsAccuracyConfirmed { get; set; }
}
