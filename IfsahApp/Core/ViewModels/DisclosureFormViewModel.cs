using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Http;
using IfsahApp.Core.Models;
using System.Globalization;
using System.Resources;
using System.Reflection;

namespace IfsahApp.Core.ViewModels
{
    // نستخدم IValidatableObject للتحقق بين التاريخين عند إدخالهما
    public class DisclosureFormViewModel : IValidatableObject
    {
        public DisclosureFormViewModel()
        {
            Attachments = new List<IFormFile>();
            SuspectedPersons = new List<SuspectedPerson>();
            RelatedPersons = new List<RelatedPerson>();
            SavedAttachmentPaths = new List<string>();
        }

        public int Step { get; set; }

        [AllowedExtensions(new[] { ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".mp4", ".mov", ".avi", ".wmv", ".mkv", ".pdf", ".doc", ".docx", ".xls", ".xlsx", ".ppt", ".pptx" }, ErrorMessage = "FileInvalidExtension")]
        [MaxFileSize(10 * 1024 * 1024, ErrorMessage = "FileMaxSize")] // 10MB
        [JsonIgnore] // Prevent from being serialized
        public List<IFormFile>? Attachments { get; set; }

        public List<string> SavedAttachmentPaths { get; set; }

        public int Id { get; set; }

        public string DisclosureNumber { get; set; } = string.Empty;

        [Required(ErrorMessage = "DescriptionRequired")]
        [MaxLength(300, ErrorMessage = "DescriptionMaxLength")]
        public string Description { get; set; } = string.Empty;

        [MaxLength(100, ErrorMessage = "LocationMaxLength")]
        public string? Location { get; set; }

        [Required]
        public int DisclosureTypeId { get; set; }

        [Display(Name = "IncidentStartDateLabel")]
        [DataType(DataType.Date)]
        [Required(ErrorMessage = "IncidentStartDateRequired")]
        public DateTime? IncidentStartDate { get; set; }

        [Display(Name = "IncidentEndDateLabel")]
        [DataType(DataType.Date)]
        [Required(ErrorMessage = "IncidentEndDateRequired")]
        public DateTime? IncidentEndDate { get; set; }

        [Required]
        public int SubmittedById { get; set; }

        public List<SuspectedPerson> SuspectedPersons { get; set; }
        public List<RelatedPerson> RelatedPersons { get; set; }

        // ✅ FIXED: replaced NotImplementedException with working validation
        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        var results = new List<ValidationResult>();

        var assembly = typeof(DisclosureFormViewModel).Assembly;
        var resourceManager = new ResourceManager("IfsahApp.Resources.Core.ViewModels.DisclosureFormViewModel", assembly);
        var culture = CultureInfo.CurrentUICulture;

        string GetMessage(string key)
        {
            return resourceManager.GetString(key, culture) ?? key;
        }

        if (IncidentStartDate.HasValue && IncidentEndDate.HasValue)
        {
            var start = IncidentStartDate.Value.Date;
            var end = IncidentEndDate.Value.Date;
            var today = DateTime.UtcNow.Date;

            // 1️⃣ Start date > End date
            if (start > end)
            {
                results.Add(new ValidationResult(
                    GetMessage("StartDateAfterEndDate"),
                    new[] { nameof(IncidentStartDate), nameof(IncidentEndDate) }
                ));
            }

            // 2️⃣ Future dates not allowed
            if (start > today || end > today)
            {
                results.Add(new ValidationResult(
                    GetMessage("FutureDatesNotAllowed"),
                    new[] { nameof(IncidentStartDate), nameof(IncidentEndDate) }
                ));
            }
        }

        return results;
     }

    }

    // MaxFileSize Attribute
    public class MaxFileSizeAttribute : ValidationAttribute
    {
        private readonly long _maxBytes;

        public MaxFileSizeAttribute(long maxBytes)
        {
            _maxBytes = maxBytes;
        }

        protected override ValidationResult IsValid(object value, ValidationContext validationContext)
        {
            if (value == null) return ValidationResult.Success;

            IEnumerable<IFormFile> files = value switch
            {
                IFormFile f => new[] { f },
                IEnumerable<IFormFile> list => list,
                _ => null
            };

            if (files == null) return ValidationResult.Success;

            foreach (var file in files)
            {
                if (file == null) continue;

                if (file.Length > _maxBytes)
                {
                    double maxMb = Math.Round((double)_maxBytes / (1024 * 1024), 2);

                    string template = GetLocalizedMessage("FileMaxSize", validationContext);
                    var msg = string.Format(CultureInfo.CurrentCulture, template, file.FileName, maxMb);
                    return new ValidationResult(msg);
                }
            }

            return ValidationResult.Success;
        }

        private string GetLocalizedMessage(string key, ValidationContext validationContext)
        {
            try
            {
                var assembly = typeof(DisclosureFormViewModel).Assembly;
                var resourceManager = new ResourceManager("IfsahApp.Resources.Core.ViewModels.DisclosureFormViewModel", assembly);

                var culture = CultureInfo.CurrentUICulture;
                var message = resourceManager.GetString(key, culture);

                return message ?? GetDefaultMessage(key);
            }
            catch
            {
                return GetDefaultMessage(key);
            }
        }

        private string GetDefaultMessage(string key)
        {
            return key switch
            {
                "FileInvalidExtension" => "File {0} has an invalid extension. Allowed extensions: {1}.",
                "FileMaxSize" => "File {0} exceeds the maximum allowed size of {1} MB.",
                _ => "Validation error."
            };
        }
    }

    // AllowedExtensions Attribute
    public class AllowedExtensionsAttribute : ValidationAttribute
    {
        private readonly HashSet<string> _allowed;

        public AllowedExtensionsAttribute(string[] extensions)
        {
            _allowed = new HashSet<string>(extensions.Select(e => e.ToLowerInvariant()));
        }

        protected override ValidationResult IsValid(object value, ValidationContext validationContext)
        {
            if (value == null) return ValidationResult.Success;

            IEnumerable<IFormFile> files = value switch
            {
                IFormFile f => new[] { f },
                IEnumerable<IFormFile> list => list,
                _ => null
            };

            if (files == null) return ValidationResult.Success;

            foreach (var file in files)
            {
                if (file == null) continue;

                var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
                if (!_allowed.Contains(ext))
                {
                    string allowedList = string.Join(", ", _allowed);

                    string template = GetLocalizedMessage("FileInvalidExtension", validationContext);
                    var msg = string.Format(CultureInfo.CurrentCulture, template, file.FileName, allowedList);
                    return new ValidationResult(msg);
                }
            }

            return ValidationResult.Success;
        }

        private string GetLocalizedMessage(string key, ValidationContext validationContext)
        {
            try
            {
                var assembly = typeof(DisclosureFormViewModel).Assembly;
                var resourceManager = new ResourceManager("IfsahApp.Resources.Core.ViewModels.DisclosureFormViewModel", assembly);

                var culture = CultureInfo.CurrentUICulture;
                var message = resourceManager.GetString(key, culture);

                return message ?? GetDefaultMessage(key);
            }
            catch
            {
                return GetDefaultMessage(key);
            }
        }

        private string GetDefaultMessage(string key)
        {
            return key switch
            {
                "FileInvalidExtension" => "File {0} has an invalid extension. Allowed extensions: {1}.",
                "FileMaxSize" => "File {0} exceeds the maximum allowed size of {1} MB.",
                _ => "Validation error."
            };
        }
    }
}
