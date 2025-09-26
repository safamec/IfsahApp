using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using IfsahApp.Utils;

namespace IfsahApp.Core.Models;

public class DisclosureAttachment
{
    public int Id { get; set; }

    [Required]
    public int DisclosureId { get; set; }

    public Disclosure? Disclosure { get; set; }

    [Required]
    public string FileName { get; set; } = string.Empty; // الاسم المخزن (GUID)

    // ✅ الاسم الأصلي اللي رفعه المستخدم
    public string? OriginalFileName { get; set; }

    public string? FileType { get; set; }

    public long FileSize { get; set; }

    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;

    [NotMapped]
    public string FullPath => FilePathHelper.GetAttachmentPath(FileName, FileType ?? "");
}
