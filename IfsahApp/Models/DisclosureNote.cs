using System.ComponentModel.DataAnnotations;

namespace IfsahApp.Models;

public class DisclosureNote
{
    public int Id { get; set; }

    [Required]
    public int DisclosureId { get; set; }
    public Disclosure Disclosure { get; set; }

    [Required]
    public int AuthorId { get; set; }
    public User Author { get; set; }

    [Required, MaxLength(2000)]
    public string Content { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
