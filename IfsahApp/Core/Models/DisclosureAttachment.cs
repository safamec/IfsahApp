using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using IfsahApp.Utils;

namespace IfsahApp.Core.Models;

public class DisclosureAttachment
{
    public int Id { get; set; }

    [Required]
    public int DisclosureId { get; set; }

    // Nullable to satisfy EF Core materialization and avoid CS8618
    public Disclosure? Disclosure { get; set; }

    [Required]
    public string FileName { get; set; } = string.Empty;

    // Optional: FileType (e.g., pdf, docx, jpg, etc.)
    public string? FileType { get; set; }

    // File size in bytes
    public long FileSize { get; set; }

    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;

    // Not stored in DB — dynamically built using appsettings.json
    [NotMapped]
    public string FullPath => FilePathHelper.GetAttachmentPath(FileName, FileType ?? "");
}
