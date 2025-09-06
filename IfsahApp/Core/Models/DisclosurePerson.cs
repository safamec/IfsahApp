using System.ComponentModel.DataAnnotations;

namespace IfsahApp.Models;

public abstract class DisclosurePerson
{
    public int Id { get; set; }
    [Required]
    public int DisclosureId { get; set; }
    public Disclosure Disclosure { get; set; }
    [Required]
    public string Name { get; set; }
    [EmailAddress]
    public string Email { get; set; }
    public string Phone { get; set; }
    public string Organization { get; set; }
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