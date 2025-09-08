using System.ComponentModel.DataAnnotations;
using IfsahApp.Core.Models;
using Microsoft.AspNetCore.Http; // for IFormFile

namespace IfsahApp.Core.ViewModels;

public class DisclosureFormViewModel
{
    public DisclosureFormViewModel()
    {
        Attachments = new List<IFormFile>();
        SuspectedPersons = new List<SuspectedPerson>();
        RelatedPersons = new List<RelatedPerson>();
    }

    [Required]
    public List<IFormFile> Attachments { get; set; }

    public int Id { get; set; }

    public string DisclosureNumber { get; set; } = string.Empty;

    [Required, MaxLength(300)]
    public string Description { get; set; } = string.Empty;

    [MaxLength(100)]
    public string? Location { get; set; }

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
}
