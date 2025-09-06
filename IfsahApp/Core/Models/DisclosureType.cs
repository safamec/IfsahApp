using System.ComponentModel.DataAnnotations;

namespace IfsahApp.Models;

/// <summary>
/// Represents a category or type of disclosure (e.g., Conflict of Interest, Misconduct, etc.)
/// </summary>
public class DisclosureType
{
    public int Id { get; set; }

    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation property — initialized to prevent null reference issues
    public ICollection<Disclosure> Disclosures { get; set; } = new List<Disclosure>();
}
