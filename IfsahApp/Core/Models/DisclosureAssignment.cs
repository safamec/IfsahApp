using System.ComponentModel.DataAnnotations;

namespace IfsahApp.Core.Models;

public class DisclosureAssignment
{
    public int Id { get; set; }

    [Required]
    public int DisclosureId { get; set; }

    public Disclosure? Disclosure { get; set; } // nullable nav property

    [Required]
    public int ExaminerId { get; set; }

    public User? Examiner { get; set; } // nullable nav property

    public DateTime AssignedAt { get; set; } = DateTime.UtcNow;

    public string Status { get; set; } = "Assigned"; // Assigned, InProgress, Returned, Closed
}