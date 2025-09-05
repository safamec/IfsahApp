using System.ComponentModel.DataAnnotations;

namespace IfsahApp.Models;

public class UserNotificationPreference
{
    public int Id { get; set; }

    [Required]
    public int UserId { get; set; }
    public User User { get; set; }

    public bool NotifyOnSubmission { get; set; } = true;
    public bool NotifyOnAssignment { get; set; } = true;
    public bool NotifyOnReview { get; set; } = true;

    public bool ViaEmail { get; set; } = true;
    public bool ViaSystem { get; set; } = true;
}
