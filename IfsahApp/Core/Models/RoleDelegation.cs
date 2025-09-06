using System.ComponentModel.DataAnnotations;

namespace IfsahApp.Core.Models;

public class RoleDelegation
{
    public int Id { get; set; }

    public int FromUserId { get; set; }
    public User? FromUser { get; set; } // nullable nav property

    public int ToUserId { get; set; }
    public User? ToUser { get; set; } // nullable nav property

    [Required]
    public string Role { get; set; } = string.Empty; // initialized to avoid null

    public bool IsPermanent { get; set; } = false;

    [Required]
    public DateTime StartDate { get; set; }

    public DateTime? EndDate { get; set; }

    public string? Reason { get; set; } // optional

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}