using IfsahApp.Configuration;

namespace IfsahApp.Helpers;

public static class FilePathHelper
{
    public static string GetAttachmentPath(string fileName, string fileType)
    {
        var basePath = AppSettings.BaseAttachmentPath;
        return Path.Combine(basePath, "Attachments", $"{fileName}.{fileType}");
    }
}
