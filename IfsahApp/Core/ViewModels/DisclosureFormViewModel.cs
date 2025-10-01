using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Http;
using IfsahApp.Core.Models;

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

        // المرفقات "اختيارية"؛ التحقق يشتغل فقط لو فيه ملفات
        [AllowedExtensions(new[] {
            ".jpg", ".jpeg", ".png", ".gif", ".bmp",
            ".mp4", ".mov", ".avi", ".wmv", ".mkv",
            ".pdf", ".doc", ".docx", ".xls", ".xlsx", ".ppt", ".pptx"
        })]
        [MaxFileSize(10 * 1024 * 1024)] // 10MB
        [JsonIgnore]
        public List<IFormFile> Attachments { get; set; }

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

        // تحقق المدى: يعمل فقط إذا أُدخِل التاريخان
        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (IncidentStartDate.HasValue && IncidentEndDate.HasValue)
            {
                if (IncidentEndDate.Value.Date < IncidentStartDate.Value.Date)
                {
                    yield return new ValidationResult(
                        "Incident end date cannot be earlier than the start date.",
                        new[] { nameof(IncidentStartDate), nameof(IncidentEndDate) }
                    );
                }
            }
        }
    }

    // أقصى حجم ملف
    public class MaxFileSizeAttribute : ValidationAttribute
    {
        private readonly int _maxFileSize;
        public MaxFileSizeAttribute(int maxFileSize) => _maxFileSize = maxFileSize;

        protected override ValidationResult IsValid(object value, ValidationContext validationContext)
        {
            var files = value as IEnumerable<IFormFile>;
            if (files == null || !files.Any()) return ValidationResult.Success; // اختياري

            foreach (var file in files)
            {
                if (file.Length > _maxFileSize)
                {
                    return new ValidationResult(
                        $"File {file.FileName} exceeds the maximum allowed size of {_maxFileSize / (1024 * 1024)} MB.");
                }
            }
            return ValidationResult.Success;
        }
    }

    // الامتدادات المسموحة
    public class AllowedExtensionsAttribute : ValidationAttribute
    {
        private readonly string[] _extensions;
        public AllowedExtensionsAttribute(string[] extensions) => _extensions = extensions;

        protected override ValidationResult IsValid(object value, ValidationContext validationContext)
        {
            var files = value as IEnumerable<IFormFile>;
            if (files == null || !files.Any()) return ValidationResult.Success; // اختياري

            foreach (var file in files)
            {
                var extension = Path.GetExtension(file.FileName);
                if (string.IsNullOrEmpty(extension) ||
                    !_extensions.Contains(extension.ToLowerInvariant()))
                {
                    return new ValidationResult(
                        $"File {file.FileName} has an invalid extension. Allowed: {string.Join(", ", _extensions)}.");
                }
            }
            return ValidationResult.Success;
        }
    }
}
