namespace IfsahApp.Infrastructure.Settings;

public class AttachmentSettings
{
    /// <summary>
    /// The base path for saving uploaded attachments.
    /// Example:
    /// - Development: "uploads"
    /// - Staging: "D://IfsahAppStg/Upload/"
    /// - Production: "/var/www/ifsahapp/uploads/"
    /// </summary>
    public string BasePath { get; set; } = "uploads";
}

