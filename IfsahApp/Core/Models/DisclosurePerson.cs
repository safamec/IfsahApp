using System.ComponentModel.DataAnnotations;

namespace IfsahApp.Core.Models;

public abstract class DisclosurePerson
{
    public int Id { get; set; }

    [Required]
    public int DisclosureId { get; set; }

    public Disclosure? Disclosure { get; set; } // nullable navigation property

    [Required]
    public string Name { get; set; } = string.Empty; // initialized to avoid null

    [EmailAddress]
    public string? Email { get; set; } // optional

    public string? Phone { get; set; } // optional

    public string? Organization { get; set; } // optional
}

// Derived classes representing different person types
public class SuspectedPerson : DisclosurePerson
{
    // add extra properties if needed
}

public class RelatedPerson : DisclosurePerson
{
    // add extra properties if needed
}
