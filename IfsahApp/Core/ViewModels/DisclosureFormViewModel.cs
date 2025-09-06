using System.ComponentModel.DataAnnotations;
using IfsahApp.Core.Models;

namespace IfsahApp.Core.ViewModels;

public class DisclosureFormViewModel
{
    public DisclosureFormViewModel()
    {
        Attachments = [];
        SuspectedPersons = [];
        RelatedPersons = [];
    }

    [Required]
    public List<IFormFile> Attachments { get; set; }

    public int Id { get; set; }

    [Required]
    public string DisclosureNumber { get; set; } = string.Empty; // required, initialized

    [Required, MaxLength(300)]
    public string Description { get; set; } = string.Empty;

    [MaxLength(100)]
    public string? Location { get; set; } // optional

    [Required]
    public int DisclosureTypeId { get; set; }

    [Display(Name = "Incident Start Date")]
    [DataType(DataType.Date)]
    [Required(ErrorMessage = "Incident start date is required.")]
    public DateTime? IncidentStartDate { get; set; }

    [Display(Name = "Incident End Date")]
    [DataType(DataType.Date)]
    [Required(ErrorMessage = "Incident end date is required.")]
    public DateTime? IncidentEndDate { get; set; }

    [Required]
    public int SubmittedById { get; set; }

    public List<SuspectedPerson> SuspectedPersons { get; set; }

    public List<RelatedPerson> RelatedPersons { get; set; }

    public Disclosure? Disclosure { get; internal set; } // can be null
}
