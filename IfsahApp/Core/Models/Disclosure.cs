using System.ComponentModel.DataAnnotations;
using IfsahApp.Core.Enums;

namespace IfsahApp.Core.Models;

public class Disclosure
{
    public int Id { get; set; }

    [Required]
    public string DisclosureNumber { get; set; } = string.Empty; // e.g., DISC-2025-0001

    [Required, MaxLength(300)]
    public string Description { get; set; } = string.Empty;

    [Display(Name = "Incident Start Date")]
    [DataType(DataType.Date)]
    [Required(ErrorMessage = "Incident start date is required.")]
    public DateTime? IncidentStartDate { get; set; }

    [Display(Name = "Incident End Date")]
    [DataType(DataType.Date)]
    public DateTime? IncidentEndDate { get; set; }

    [MaxLength(100)]
    public string? Location { get; set; } // optional

    [Required]
    public DateTime SubmittedAt { get; set; } = DateTime.UtcNow;

    public DisclosureStatus Status { get; set; } = DisclosureStatus.New;

    [Required]
    public int DisclosureTypeId { get; set; }

    public DisclosureType? DisclosureType { get; set; } // nullable nav property

    [Required]
    public int SubmittedById { get; set; }

    public User? SubmittedBy { get; set; } // nullable nav property

    public int? AssignedToUserId { get; set; }

    public User? AssignedToUser { get; set; } // nullable nav property

    // Strongly typed collections
    public ICollection<SuspectedPerson> SuspectedPeople { get; set; } = new List<SuspectedPerson>();
    public ICollection<RelatedPerson> RelatedPeople { get; set; } = new List<RelatedPerson>();
    public ICollection<DisclosureAttachment> Attachments { get; set; } = new List<DisclosureAttachment>();
    public ICollection<DisclosureNote> Notes { get; set; } = new List<DisclosureNote>();
    public ICollection<DisclosureAssignment> Assignments { get; set; } = new List<DisclosureAssignment>();
    public DisclosureReview? FinalReview { get; set; } // nullable, may not exist

    // Admin comments
    public ICollection<Comment> Comments { get; set; } = new List<Comment>();

    public bool IsAccuracyConfirmed { get; set; }
}

