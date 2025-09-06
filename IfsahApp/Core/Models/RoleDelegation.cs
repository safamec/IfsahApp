using System.ComponentModel.DataAnnotations;

namespace IfsahApp.Models;

public class RoleDelegation
{
    public int Id { get; set; }

    public int FromUserId { get; set; }
    public User FromUser { get; set; }

    public int ToUserId { get; set; }
    public User ToUser { get; set; }

    [Required]
    public string Role { get; set; } // AuditManager or Examiner only

    public bool IsPermanent { get; set; } = false;

    [Required]
    public DateTime StartDate { get; set; }

    public DateTime? EndDate { get; set; }

    public string Reason { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
