using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Http;
using IfsahApp.Core.Models;
using System.IO; // Needed for Path.GetExtension
using System.Linq; // Needed for Contains in AllowedExtensions

namespace IfsahApp.Core.ViewModels
{
    public class DisclosureFormViewModel
    {
        public DisclosureFormViewModel()
        {
            Attachments = new List<IFormFile>();
            SuspectedPersons = new List<SuspectedPerson>();
            RelatedPersons = new List<RelatedPerson>();
            SavedAttachmentPaths = new List<string>();
        }
        public int Step { get; set; } 


        [AllowedExtensions(new[] { ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".mp4", ".mov", ".avi", ".wmv", ".mkv", ".pdf", ".doc", ".docx", ".xls", ".xlsx", ".ppt", ".pptx" })]
        [MaxFileSize(10 * 1024 * 1024)] // 10MB
        [JsonIgnore] // Prevent from being serialized
        public List<IFormFile> Attachments { get; set; }

        public List<string> SavedAttachmentPaths { get; set; }

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

    // MaxFileSize Attribute
    public class MaxFileSizeAttribute : ValidationAttribute
    {
        private readonly int _maxFileSize;

        public MaxFileSizeAttribute(int maxFileSize)
        {
            _maxFileSize = maxFileSize;
        }

        protected override ValidationResult IsValid(object value, ValidationContext validationContext)
        {
            var files = value as IEnumerable<IFormFile>;
            if (files != null)
            {
                foreach (var file in files)
                {
                    if (file.Length > _maxFileSize)
                    {
                        return new ValidationResult($"File {file.FileName} exceeds the maximum allowed size of {_maxFileSize / (1024 * 1024)} MB.");
                    }
                }
            }

            return ValidationResult.Success;
        }
    }

    // AllowedExtensions Attribute
    public class AllowedExtensionsAttribute : ValidationAttribute
    {
        private readonly string[] _extensions;

        public AllowedExtensionsAttribute(string[] extensions)
        {
            _extensions = extensions;
        }

        protected override ValidationResult IsValid(object value, ValidationContext validationContext)
        {
            var files = value as IEnumerable<IFormFile>;
            if (files != null)
            {
                foreach (var file in files)
                {
                    var extension = Path.GetExtension(file.FileName);
                    if (string.IsNullOrEmpty(extension) || !_extensions.Contains(extension.ToLower()))
                    {
                        return new ValidationResult($"File {file.FileName} has an invalid extension. Allowed extensions: {string.Join(", ", _extensions)}.");
                    }
                }
            }

            return ValidationResult.Success;
        }
    }
}
