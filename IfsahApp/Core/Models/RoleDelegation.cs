// Core/Models/RoleDelegation.cs
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace IfsahApp.Core.Models;

public class RoleDelegation
{
    public int Id { get; set; }

    [Required]
    public int FromUserId { get; set; }
    public User? FromUser { get; set; }

    [Required]
    public int ToUserId { get; set; }
    public User? ToUser { get; set; }

    [Required]
    public string Role { get; set; } = string.Empty; // e.g. "Admin"

    public bool IsPermanent { get; set; } = false;

    [Required]
    public DateTime StartDate { get; set; }

    public DateTime? EndDate { get; set; }

    public string? Reason { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [NotMapped]
    public bool IsActiveUtc =>
        StartDate <= DateTime.UtcNow && (EndDate == null || EndDate >= DateTime.UtcNow);
}
