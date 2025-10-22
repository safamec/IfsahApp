using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using IfsahApp.Config;

namespace IfsahApp.Utils
{
    public static class FilePathHelper
    {
        public static string GetUploadsFolderPath(IWebHostEnvironment env)
        {
            var uploadsFolder = Path.Combine(env.WebRootPath, AppSettings.BaseAttachmentPath);
            Directory.CreateDirectory(uploadsFolder);
            return uploadsFolder;
        }

        public static string GenerateUniqueFileName(string extension)
        {
            return $"{Guid.NewGuid()}{extension}";
        }

        public static async Task<(string? fileName, string? errorMessage)> SaveFileAsync(IFormFile file, IWebHostEnvironment env)
        {
            try
            {
                if (file == null || file.Length == 0)
                    return (null, "File is empty.");

                var extension = Path.GetExtension(file.FileName)?.ToLowerInvariant();
                if (string.IsNullOrEmpty(extension))
                    return (null, "File has no extension.");

                if (!AppSettings.AllowedExtensions.Contains(extension))
                    return (null, $"File type '{extension}' is not allowed.");

                if (file.Length > AppSettings.MaxFileSize)
                {
                    var maxMB = AppSettings.MaxFileSize / (1024 * 1024);
                    return (null, $"File '{file.FileName}' exceeds the {maxMB} MB size limit.");
                }

                var uniqueFileName = GenerateUniqueFileName(extension);
                var uploadsFolder = GetUploadsFolderPath(env);
                var filePath = Path.Combine(uploadsFolder, uniqueFileName);

                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await file.CopyToAsync(stream);
                }

                return (uniqueFileName, null);
            }
            catch (Exception ex)
            {
                // Catch any unexpected errors and return as error message
                return (null, $"Unexpected error: {ex.Message}");
            }
        }

        public static string GetAttachmentPath(string fileName, string fileType)
        {
            var folderPath = AppSettings.BaseAttachmentPath;

            return Path.Combine(folderPath, $"{fileName}.{fileType}");
        }
    }
}
