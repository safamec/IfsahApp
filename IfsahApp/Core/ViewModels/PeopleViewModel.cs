using System.ComponentModel.DataAnnotations;
using IfsahApp.Core.Enums;

namespace IfsahApp.Core.ViewModels;

public class PeopleViewModel
{
    [Required]
     public string Name { get; set; } = string.Empty; // initialized to avoid null

    [EmailAddress]
    public string? Email { get; set; } // optional

    [Phone]
    public string? Phone { get; set; } // optional

    public string? Organization { get; set; } // optional
    public PeopleType Type { get; internal set; }
}