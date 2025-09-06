using System.ComponentModel.DataAnnotations;

namespace IfsahApp.Models;

public class DisclosureAssignment
{
    public int Id { get; set; }

    [Required]
    public int DisclosureId { get; set; }
    public Disclosure Disclosure { get; set; }

    [Required]
    public int ExaminerId { get; set; }
    public User Examiner { get; set; }

    public DateTime AssignedAt { get; set; } = DateTime.UtcNow;

    public string Status { get; set; } = "Assigned"; // Assigned, InProgress, Returned, Closed
}
