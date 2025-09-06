using System.ComponentModel.DataAnnotations;

namespace IfsahApp.Core.Models;

public class DisclosureNote
{
    public int Id { get; set; }

    [Required]
    public int DisclosureId { get; set; }

    public Disclosure? Disclosure { get; set; } // nullable navigation property

    [Required]
    public int AuthorId { get; set; }

    public User? Author { get; set; } // nullable navigation property

    [Required, MaxLength(2000)]
    public string Content { get; set; } = string.Empty; // initialized to avoid null

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}