using System;
using System.Collections.Generic;
using Microsoft.Extensions.Configuration;

namespace IfsahApp.Config
{
    public static class AppSettings
    {
        private static readonly IConfigurationRoot _configuration;

        static AppSettings()
        {
            _configuration = new ConfigurationBuilder()
                .SetBasePath(AppContext.BaseDirectory)
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .AddJsonFile($"appsettings.{Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production"}.json", optional: true)
                .Build();
        }

        /// <summary>
        /// Base folder path to save attachments.
        /// Defaults to 'wwwroot/uploads' if missing.
        /// </summary>
        public static string BaseAttachmentPath =>
            _configuration.GetValue<string>("FileUploadSettings:BasePath") ?? "uploads";

        /// <summary>
        /// Allowed file extensions.
        /// Defaults to common file types if missing.
        /// </summary>
        public static List<string> AllowedExtensions =>
            _configuration.GetSection("FileUploadSettings:AllowedExtensions").Get<List<string>>()
            ?? new List<string> { ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".pdf", ".doc", ".docx" };

        /// <summary>
        /// Maximum allowed file size in bytes.
        /// Defaults to 5 MB if missing.
        /// </summary>
        public static long MaxFileSize =>
            _configuration.GetValue<long?>("FileUploadSettings:MaxFileSize") ?? 5 * 1024 * 1024;
    }
}
