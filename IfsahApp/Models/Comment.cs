using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace IfsahApp.Models
{
    public class Comment
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int DisclosureId { get; set; }

        [ForeignKey("DisclosureId")]
        public Disclosure Disclosure { get; set; }

        [Required]
        public string Text { get; set; } = string.Empty;

        // ✅ Link to the admin (User) who made the comment
        [Required]
        public int AuthorId { get; set; }

        [ForeignKey("AuthorId")]
        public User Author { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
