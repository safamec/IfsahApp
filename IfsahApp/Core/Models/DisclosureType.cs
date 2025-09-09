using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Globalization;


namespace IfsahApp.Core.Models;

public class DisclosureType
{
    public int Id { get; set; }

    [Required]
    [MaxLength(100)]
    public string ArabicName { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    public string EnglishName { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<Disclosure> Disclosures { get; set; } = new List<Disclosure>();

    [NotMapped]
    public string DisplayName =>
        CultureInfo.CurrentCulture.TwoLetterISOLanguageName == "ar"
            ? ArabicName
            : EnglishName;
}
